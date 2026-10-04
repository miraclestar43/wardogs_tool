using System.Diagnostics;
using System.Globalization;
using System.Windows.Automation;

namespace WardogsTool.InputProbe;

/// <summary>
/// C#-only: F9 cycles OFF → Large 510 → Small/Medium 310 → OFF, one step per physical press;
/// switching releases the button first; Esc returns to OFF from any state; the Hammer page and
/// the status bar show the state.
/// </summary>
internal static class F9Scenarios
{
    private const string AppTitle = "WARDOGS Tool · 战狗土木 / 建造辅助工具";

    private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Status(AutomationElement root)
    {
        foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
        {
            var n = e.Current.Name ?? "";
            if (n.StartsWith("运行中", StringComparison.Ordinal) || n.StartsWith("已停止", StringComparison.Ordinal))
                return n;
        }
        return "?";
    }

    private static string Badge(AutomationElement root)
    {
        foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
        {
            var n = e.Current.Name ?? "";
            if (n.StartsWith("RUNNING", StringComparison.Ordinal) || n.StartsWith("OFF ·", StringComparison.Ordinal))
                return n;
        }
        return "?";
    }

    private static double MeanHold(List<InputEvent> evs)
    {
        var holds = new List<double>();
        for (var i = 0; i + 1 < evs.Count; i++)
            if (evs[i].IsLeftDown && evs[i + 1].IsLeftUp)
                holds.Add(evs[i + 1].Ms - evs[i].Ms);
        if (holds.Count > 0) holds.RemoveAt(holds.Count - 1); // last hold may be cut by the next press
        return holds.Count == 0 ? double.NaN : holds.Average();
    }

    private static bool NoDoubleDown(List<InputEvent> evs)
    {
        for (var i = 1; i < evs.Count; i++)
            if (evs[i].IsLeftDown && evs[i - 1].IsLeftDown)
                return false;
        return true;
    }

    public static ScenarioResult Cycle(Probe p, string exe)
    {
        var r = new ScenarioResult("C#", "F9 cycle OFF → 大锤 510 → 小/中锤 310 → OFF; Esc → OFF from any state");
        var proc = new CSharpTarget(exe).Launch(new TargetSettings(HoldMs: 310));
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException("C# window did not appear");
            p.Sleep(1000);
            var root = AutomationElement.FromHandle(Native.FindWindowW(null, AppTitle));
            r.Metric("0 · status at start", Status(root));
            r.Expect(Status(root).StartsWith("已停止   F9 敲锤：关", StringComparison.Ordinal), "status does not show the hammer OFF at start");

            // Press 1: OFF → Large 510 (even though 310 is the selected radio button).
            p.Activate();
            var t1 = p.KeyTap(Probe.VkF9);
            p.Sleep(1700);
            var step1 = p.AppMouse(t1);
            r.Metric("1 · after 1st F9: mean hold / status / badge", $"{F(MeanHold(step1))} ms / {Status(root)} / {Badge(root)}");
            r.Expect(Math.Abs(MeanHold(step1) - 510) < 5, "1st F9 did not start the 510 ms large hammer");
            r.Expect(Status(root).Contains("F9 敲锤：大锤 510 ms"), "status does not show 大锤 510 ms");
            r.Expect(Badge(root) == "RUNNING · 大锤 510 ms", "Hammer page badge does not show 大锤 510 ms");

            // Press 2: Large → Small/Medium 310, released first.
            var d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
            if (d is not null) p.SleepUntil(d.Value.Ticks + Probe.Ms(200)); // mid-hold
            var t2 = p.KeyTap(Probe.VkF9);
            p.Sleep(1700);
            var step2 = p.AppMouse(t2);
            r.Metric("2 · after 2nd F9: first events / mean hold / status", $"{string.Join(",", step2.Take(2).Select(e => e.IsLeftUp ? "up" : "down"))} / {F(MeanHold(step2.Skip(1).ToList()))} ms / {Status(root)}");
            r.Expect(step2.Count >= 2 && step2[0].IsLeftUp && step2[1].IsLeftDown, "switch did not release the button before the 310 ms cycle");
            r.Expect(step2.Count > 0 && Recorder.ToMs(step2[0].Ticks - t2) < 40, "switch release was not prompt");
            r.Expect(Math.Abs(MeanHold(step2.Skip(1).ToList()) - 310) < 5, "2nd F9 did not switch to 310 ms");
            r.Expect(Status(root).Contains("F9 敲锤：小/中锤 310 ms"), "status does not show 小/中锤 310 ms");
            r.Expect(Badge(root) == "RUNNING · 小/中锤 310 ms", "Hammer page badge does not show 小/中锤 310 ms");

            // Press 3: Small/Medium → OFF.
            d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
            if (d is not null) p.SleepUntil(d.Value.Ticks + Probe.Ms(100));
            var t3 = p.KeyTap(Probe.VkF9);
            p.Sleep(700);
            var step3 = p.AppMouse(t3);
            r.Metric("3 · after 3rd F9: events / status", $"{string.Join(",", step3.Select(e => e.IsLeftUp ? "up" : "down"))} / {Status(root)}");
            r.Expect(step3.Count == 1 && step3[0].IsLeftUp, "3rd F9 did not release and stop");
            r.Expect(Status(root).StartsWith("已停止   F9 敲锤：关", StringComparison.Ordinal), "status does not show OFF after the 3rd F9");
            r.Expect(Badge(root) == "OFF · 已停止", "Hammer page badge does not show OFF");

            // Holding F9 (auto-repeat) advances exactly one step: OFF → 510, not further.
            var t4 = p.KeyMake(Probe.VkF9);
            for (var i = 0; i < 25; i++) { p.Sleep(33); p.KeyMake(Probe.VkF9); }
            p.KeyBreak(Probe.VkF9);
            p.Sleep(1500);
            var step4 = p.AppMouse(t4);
            r.Metric("4 · F9 held ~0.8 s (auto-repeat): mean hold / status", $"{F(MeanHold(step4))} ms / {Status(root)}");
            r.Expect(Math.Abs(MeanHold(step4) - 510) < 5 && NoDoubleDown(step4), "auto-repeat advanced more than one step");

            // Esc from Large → OFF at once.
            d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
            if (d is not null) p.SleepUntil(d.Value.Ticks + Probe.Ms(100));
            var tEsc1 = p.KeyTap(Probe.VkEsc);
            p.Sleep(600);
            var esc1 = p.AppMouse(tEsc1);
            r.Metric("5 · Esc from 大锤: events / status", $"{string.Join(",", esc1.Select(e => e.IsLeftUp ? "up" : "down"))} / {Status(root)}");
            r.Expect(esc1.Count == 1 && esc1[0].IsLeftUp && Recorder.ToMs(esc1[0].Ticks - tEsc1) < 40, "Esc from 大锤 did not release and stop at once");
            r.Expect(Status(root).StartsWith("已停止   F9 敲锤：关", StringComparison.Ordinal), "status not OFF after Esc");

            // After Esc the cycle restarts from OFF: F9 → 510 again; then F9 → 310; Esc from 310 → OFF.
            var t6 = p.KeyTap(Probe.VkF9);
            p.Sleep(1200);
            r.Expect(Math.Abs(MeanHold(p.AppMouse(t6)) - 510) < 5, "after Esc, F9 did not start again at 大锤 510");
            var t7 = p.KeyTap(Probe.VkF9);
            p.Sleep(900);
            r.Expect(Status(root).Contains("小/中锤 310 ms"), "second F9 after Esc did not reach 310");
            var tEsc2 = p.KeyTap(Probe.VkEsc);
            p.Sleep(600);
            var esc2 = p.AppMouse(tEsc2);
            r.Metric("6 · Esc from 小/中锤: events / status", $"{string.Join(",", esc2.Select(e => e.IsLeftUp ? "up" : "down"))} / {Status(root)}");
            r.Expect(esc2.Count <= 1 && esc2.All(e => e.IsLeftUp), "Esc from 小/中锤 sent more than a release");
            r.Expect(Status(root).StartsWith("已停止   F9 敲锤：关", StringComparison.Ordinal), "status not OFF after Esc from 310");

            var all = p.AppMouse(t1);
            r.Metric("whole run: downs / ups", $"{all.Count(e => e.IsLeftDown)} / {all.Count(e => e.IsLeftUp)}");
            r.Expect(all.Count(e => e.IsLeftDown) == all.Count(e => e.IsLeftUp) && NoDoubleDown(all), "a mouse-down was not released before the next one");
            r.Expect(!Probe.LeftButtonDown(), "left button still down at the end");
            r.Expect(p.KeyReachedWindow(Probe.VkF9, t1) && p.KeyReachedWindow(Probe.VkEsc, tEsc1), "F9/Esc did not reach the focused window (swallowed?)");
            _ = t7;
        }
        finally
        {
            p.Shutdown(proc);
        }
        return r;
    }
}
