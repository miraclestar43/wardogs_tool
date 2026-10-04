using System.Diagnostics;
using System.Globalization;
using System.Windows.Automation;

namespace WardogsTool.InputProbe;

/// <summary>
/// C#-only: Esc / F12 as global stop, and the F10 centre-screen magnifier.
/// </summary>
internal static class MagnifierScenarios
{
    private const string HostClass = "WardogsToolMagnifierHost";
    private const string AppTitle = "WARDOGS Tool · 战狗土木 / 建造辅助工具";

    /// <summary>
    /// The app's own window. Process.MainWindowHandle is not used here: while the lens is shown it
    /// may return the lens host instead.
    /// </summary>
    private static nint AppWindow() => Native.FindWindowW(null, AppTitle);

    private static AutomationElement? Find(AutomationElement root, ControlType type, Func<AutomationElement, bool> where, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type)))
                if (where(e)) return e;
            Thread.Sleep(100);
        } while (sw.ElapsedMilliseconds < timeoutMs);
        return null;
    }

    private static void SelectTab(AutomationElement root, string contains)
    {
        var tab = Find(root, ControlType.TabItem, e => e.Current.Name.Contains(contains))
                  ?? throw new AbortException($"tab '{contains}' not found");
        ((SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Thread.Sleep(300);
    }

    private static void Invoke(AutomationElement root, string name)
    {
        var button = Find(root, ControlType.Button, e => e.Current.Name.Contains(name))
                     ?? throw new AbortException($"button '{name}' not found");
        ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }


    private static bool KeyDownNow(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>
    /// Esc with hammer, anti-AFK and the lens all on: everything stops, everything held is released,
    /// the lens is hidden, the app keeps running. Then F12: the same cleanup, then exit.
    /// </summary>
    public static ScenarioResult EscGlobalStop(Probe p, string exe)
    {
        var r = new ScenarioResult("C#", "Esc = global stop (hammer + anti-AFK + magnifier), F12 = same + exit");
        var proc = new CSharpTarget(exe).Launch(new TargetSettings(PeriodSeconds: "1.5", GapMs: "300"));
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException("C# window did not appear");
            p.Sleep(1000);

            p.Activate();
            var t0 = p.KeyTap(Probe.VkF8);
            p.KeyTap(Probe.VkF9);
            p.KeyTap(0x79);
            var c = p.WaitFor(e => e.FromApp && e.IsKeyDown && e.Vk == Probe.VkC, t0, 4000);
            var host = Native.FindWindowW(HostClass, null);
            r.Expect(c is not null, "anti-AFK did not press while hammering");
            r.Expect(host != 0 && Native.IsWindowVisible(host), "F10 did not show the lens");
            var d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
            if (d is not null) p.SleepUntil(d.Value.Ticks + Probe.Ms(100)); // Esc lands mid-hold

            var tEsc = p.KeyTap(Probe.VkEsc);
            p.Sleep(800);
            var mouse = p.AppMouse(t0);
            var keys = p.AppKeys(t0, Probe.VkC);
            var late = mouse.Concat(keys).Where(e => e.Ticks > tEsc + Probe.Ms(120)).ToList();
            r.Metric("before Esc: mouse downs / anti-AFK presses", $"{mouse.Count(e => e.IsLeftDown && e.Ticks < tEsc)} / {keys.Count(e => e.IsKeyDown && e.Ticks < tEsc)}");
            r.Metric("events later than Esc + 120 ms", late.Count);
            r.Metric("lens visible after Esc", host != 0 && Native.IsWindowVisible(host));
            r.Metric("app still running after Esc", !proc.HasExited);
            r.Expect(late.Count == 0, "something was still sent after Esc");
            r.Expect(mouse.Count(e => e.IsLeftDown) == mouse.Count(e => e.IsLeftUp), "a mouse-down has no mouse-up");
            r.Expect(keys.Count(e => e.IsKeyDown) == keys.Count(e => e.IsKeyUp), "an anti-AFK key-down has no key-up");
            r.Expect(!Probe.LeftButtonDown() && !KeyDownNow(Probe.VkC), "a button or key is still held after Esc");
            r.Expect(host == 0 || !Native.IsWindowVisible(host), "Esc did not hide the lens");
            r.Expect(!proc.HasExited, "Esc closed the app");
            r.Expect(p.KeyReachedWindow(Probe.VkEsc, tEsc), "Esc did not reach the focused window (swallowed?)");
            r.Expect(p.KeyReachedWindow(0x79, t0), "F10 did not reach the focused window (swallowed?)");
            var root = AutomationElement.FromHandle(AppWindow());
            var status = Find(root, ControlType.Text, e => e.Current.Name.StartsWith("已停止") || e.Current.Name.StartsWith("运行中"));
            r.Metric("status line after Esc", status?.Current.Name ?? "?");
            r.Expect(status?.Current.Name.StartsWith("已停止") == true, "status does not show everything stopped");

            // F12: the same cleanup, then exit.
            p.Activate();
            var t1 = p.KeyTap(Probe.VkF9);
            p.KeyTap(0x79);
            p.Sleep(700);
            d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
            if (d is not null) p.SleepUntil(d.Value.Ticks + Probe.Ms(100));
            var tF12 = p.KeyTap(Probe.VkF12);
            var exited = proc.WaitForExit(4000);
            p.Sleep(300);
            var mouse2 = p.AppMouse(t1);
            r.Metric("F12: exited / lens window after exit", $"{exited} / {(Native.FindWindowW(HostClass, null) == 0 ? "gone" : "STILL THERE")}");
            r.Expect(exited, "F12 did not exit");
            r.Expect(mouse2.Count > 0 && mouse2[^1].IsLeftUp && mouse2[^1].Ticks > tF12, "F12 did not release the held button");
            r.Expect(mouse2.Count(e => e.IsLeftDown) == mouse2.Count(e => e.IsLeftUp), "a mouse-down has no mouse-up after F12");
            r.Expect(Native.FindWindowW(HostClass, null) == 0, "lens left behind after F12");
            r.Expect(!Probe.LeftButtonDown(), "left button still down after F12");
        }
        finally
        {
            p.Shutdown(proc);
        }
        return r;
    }

    /// <summary>Run lengths of dark/light along a row, ignoring the two partial runs at the ends.</summary>
    private static List<int> Runs(byte[] row)
    {
        var runs = new List<int>();
        var dark = row[0] < 128;
        var length = 0;
        foreach (var v in row)
        {
            if (v < 128 == dark) { length++; continue; }
            runs.Add(length);
            dark = v < 128;
            length = 1;
        }
        return runs.Skip(1).ToList(); // first run is cut by the row start
    }

    private static double Median(List<int> xs) => xs.Count == 0 ? 0 : xs.OrderBy(x => x).ElementAt(xs.Count / 2);

    private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);


    private static double Stripes(Native.POINT centre, int rowX, int rowWidth) =>
        Median(Runs(Native.CaptureRow(rowX, centre.Y, rowWidth)));

    private static void ExpectZoom(ScenarioResult r, string label, double measured, double baseline, double zoom)
    {
        r.Metric($"stripe width {label} px", F(measured));
        r.Expect(Math.Abs(measured - zoom * baseline) <= 4, $"{label}: expected ~{F(zoom * baseline)} px");
    }


    /// <summary>The magnifier on the probe window's stripe pattern.</summary>
    public static ScenarioResult Magnifier(Probe p, string exe, Native.POINT centre, Action<bool> showStripes, Func<int> clicksAtWindow)
    {
        var r = new ScenarioResult("C#", "Magnifier: F10 cycle OFF→2x→3x→4x→OFF, centred, click-through, no focus steal, no recursion");
        showStripes(true);
        var proc = new CSharpTarget(exe).Launch(new TargetSettings());
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException("C# window did not appear");
            p.Sleep(1000);
            p.Activate();
            var probeWindow = Native.GetForegroundWindow();

            // Baseline: stripe width without the lens.
            const int rowWidth = 560;
            var rowX = centre.X - rowWidth / 2;
            var baseline = Median(Runs(Native.CaptureRow(rowX, centre.Y, rowWidth)));
            r.Metric("stripe width without lens px", F(baseline));

            var tF10 = p.KeyTap(0x79);
            p.Sleep(500);
            var host = Native.FindWindowW(HostClass, null);
            r.Expect(host != 0 && Native.IsWindowVisible(host), "F10 did not show the lens");
            if (host == 0) return r;

            var ex = Native.GetWindowLongPtrW(host, Native.GWL_EXSTYLE);
            bool Has(long bit) => (ex & bit) == bit;
            r.Metric("lens ex-styles", $"topmost={Has(0x8)} click-through={Has(0x20)} layered={Has(0x80000)} no-activate={Has(0x08000000)} toolwindow(no taskbar)={Has(0x80)}");
            r.Expect(Has(0x8) && Has(0x20) && Has(0x80000) && Has(0x08000000) && Has(0x80), "lens window styles are wrong");
            Native.GetWindowRect(host, out var rect);
            r.Metric("lens rect", $"({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom})");
            r.Expect(rect.Right - rect.Left == 600 && rect.Bottom - rect.Top == 400, "lens is not 600x400");
            // The lens centres on the monitor (here the primary one, which holds the focused window).
            var monitorCentre = (X: Native.GetSystemMetrics(0) / 2, Y: Native.GetSystemMetrics(1) / 2);
            r.Metric("monitor centre", $"({monitorCentre.X},{monitorCentre.Y})");
            r.Expect(Math.Abs((rect.Left + rect.Right) / 2 - monitorCentre.X) <= 1 && Math.Abs((rect.Top + rect.Bottom) / 2 - monitorCentre.Y) <= 1,
                "lens is not centred on the monitor");
            r.Expect(Native.GetForegroundWindow() == probeWindow, "the lens (or app) took the focus");
            r.Expect(p.KeyReachedWindow(0x79, tF10), "F10 did not reach the focused window (swallowed?)");

            var zoomed = Runs(Native.CaptureRow(rowX, centre.Y, rowWidth));
            var zoomedWidth = Median(zoomed);
            var consistent = zoomed.Count(w => Math.Abs(w - 2 * baseline) <= 3) / (double)Math.Max(1, zoomed.Count);
            r.Metric("stripe width with lens at 2.0x px", F(zoomedWidth));
            r.Metric("stripes at exactly 2x width", $"{consistent:P0}");
            r.Expect(Math.Abs(zoomedWidth - 2 * baseline) <= 3, "lens does not show a 2x enlargement");
            // A lens that captured itself would show nested, wider stripes in the middle.
            r.Expect(consistent >= 0.8, "stripe widths are not uniform (recursive capture?)");

            var clicksBefore = clicksAtWindow();
            Native.mouse_event(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, Recorder.ProbeMarker);
            Thread.Sleep(30);
            Native.mouse_event(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, Recorder.ProbeMarker);
            p.Sleep(200);
            r.Metric("click at centre reached the window under the lens", clicksAtWindow() > clicksBefore);
            r.Expect(clicksAtWindow() > clicksBefore, "the lens is not click-through");

            // CPU while on.
            proc.Refresh();
            var cpu0 = proc.TotalProcessorTime;
            var sw = Stopwatch.StartNew();
            p.Sleep(3000);
            proc.Refresh();
            var cpu = (proc.TotalProcessorTime - cpu0).TotalMilliseconds / sw.Elapsed.TotalMilliseconds / Environment.ProcessorCount;
            r.Metric("WardogsTool CPU with lens on (% of machine)", (cpu * 100).ToString("0.00", CultureInfo.InvariantCulture));
            r.Expect(cpu < 0.05, "lens uses too much CPU");

            // F10 cycle: OFF → 2.0x (above) → 3.0x → 4.0x → OFF, one step per press.
            p.Activate();
            p.KeyTap(0x79);
            p.Sleep(400);
            ExpectZoom(r, "after 2nd F10 (3.0x)", Stripes(centre, rowX, rowWidth), baseline, 3.0);
            p.KeyTap(0x79);
            p.Sleep(400);
            ExpectZoom(r, "after 3rd F10 (4.0x)", Stripes(centre, rowX, rowWidth), baseline, 4.0);
            p.KeyTap(0x79);
            p.Sleep(150);
            r.Metric("lens visible 150 ms after 4th F10", Native.IsWindowVisible(host));
            r.Expect(!Native.IsWindowVisible(host), "4th F10 did not turn the lens off immediately");
            r.Expect(Math.Abs(Stripes(centre, rowX, rowWidth) - baseline) <= 2, "screen still magnified after turning it off");

            // UI-only 2.5x via the list and the on/off button; F10 then goes to the next step, 3.0x.
            var root = AutomationElement.FromHandle(AppWindow());
            SelectTab(root, "Magnifier");
            var combo = Find(root, ControlType.ComboBox, e => e.Current.AutomationId == "ZoomBox")!;
            ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
            p.Sleep(300);
            var items = combo.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
                .Cast<AutomationElement>().ToList();
            r.Metric("zoom items", string.Join(", ", items.Select(i => i.Current.Name)));
            var item = items.FirstOrDefault(i => i.Current.Name.StartsWith("2.5"));
            r.Expect(item is not null, "2.5x not found in the zoom list");
            if (item is null) return r;
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
            p.Sleep(200);
            Invoke(root, "ON / OFF");
            p.Sleep(400);
            ExpectZoom(r, "UI 2.5x", Stripes(centre, rowX, rowWidth), baseline, 2.5);
            var status = Find(root, ControlType.Text, e => e.Current.Name.StartsWith("Magnifier:"));
            r.Metric("magnifier status", status?.Current.Name ?? "?");
            p.Activate();
            p.KeyTap(0x79);
            p.Sleep(400);
            ExpectZoom(r, "F10 from 2.5x (3.0x)", Stripes(centre, rowX, rowWidth), baseline, 3.0);
            r.Metric("lens visible after F10 from 2.5x", Native.IsWindowVisible(host));
            foreach (AutomationElement w in AutomationElement.RootElement.FindAll(TreeScope.Children,
                         new PropertyCondition(AutomationElement.ProcessIdProperty, proc.Id)))
                if (w.Current.ClassName == "#32770")
                    r.Metric("dialog", string.Join(" / ", w.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                        .Cast<AutomationElement>().Select(e => e.Current.Name)));

            // Quit with the lens on: the window must go away with the app.
            Native.PostMessageW(AppWindow(), 0x0010 /* WM_CLOSE */, 0, 0);
            r.Expect(proc.WaitForExit(4000), "app did not exit");
            p.Sleep(300);
            r.Metric("lens window after app exit", Native.FindWindowW(HostClass, null) == 0 ? "gone" : "STILL THERE");
            r.Expect(Native.FindWindowW(HostClass, null) == 0, "lens window left behind after exit");

            var saved = CSharpTarget.ReadSettings();
            r.Metric("saved zoom", saved?["magnifier"]?["zoom"]?.ToString() ?? "?");
            r.Expect(saved?["magnifier"]?["zoom"]?.GetValue<double>() == 3.0, "zoom was not saved");
        }
        finally
        {
            p.Shutdown(proc);
            showStripes(false);
        }
        return r;
    }
}