using System.Diagnostics;
using System.Globalization;
using System.Windows.Automation;

namespace WardogsTool.InputProbe;

/// <summary>
/// C#-only: per-feature stop buttons, and the F10 centre-screen magnifier.
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

    /// <summary>Each tab's stop button stops only its own feature.</summary>
    public static ScenarioResult PerFeatureStop(Probe p, string exe)
    {
        var r = new ScenarioResult("C#", "Stopping one feature leaves the other running");
        var proc = new CSharpTarget(exe).Launch(new TargetSettings(PeriodSeconds: "1.5", GapMs: "300"));
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException("C# window did not appear");
            p.Sleep(1000);
            var root = AutomationElement.FromHandle(proc.MainWindowHandle);

            // Hammer stop button while both run: anti-AFK must keep pressing.
            p.Activate();
            p.KeyTap(Probe.VkF8);
            p.KeyTap(Probe.VkF9);
            p.Sleep(400);
            SelectTab(root, "Hammer");
            var tStopHammer = Recorder.Now;
            Invoke(root, "停止敲锤");
            p.Sleep(2600);
            var mouseAfter = p.AppMouse(tStopHammer + Probe.Ms(150)).Count(e => e.IsLeftDown);
            var keysAfter = p.AppKeys(tStopHammer, Probe.VkC).Count(e => e.IsKeyDown);
            r.Metric("after 停止敲锤: mouse downs / anti-AFK presses", $"{mouseAfter} / {keysAfter}");
            r.Expect(mouseAfter == 0, "hammer kept clicking after its stop button");
            r.Expect(keysAfter >= 1, "anti-AFK stopped too");
            r.Expect(!Probe.LeftButtonDown(), "left button down after hammer stop");

            // Anti-AFK stop button while both run: hammering must continue.
            p.Activate();
            p.KeyTap(Probe.VkF9);
            p.Sleep(300);
            SelectTab(root, "Anti-AFK");
            var tStopAfk = Recorder.Now;
            Invoke(root, "停止防挂机");
            p.Sleep(2600);
            var keysAfterAfk = p.AppKeys(tStopAfk + Probe.Ms(80), Probe.VkC).Count(e => e.IsKeyDown);
            var mouseAfterAfk = p.AppMouse(tStopAfk).Count(e => e.IsLeftDown);
            r.Metric("after 停止防挂机: anti-AFK presses / mouse downs", $"{keysAfterAfk} / {mouseAfterAfk}");
            r.Expect(keysAfterAfk == 0, "anti-AFK kept pressing after its stop button");
            r.Expect(mouseAfterAfk >= 4, "hammer stopped too");
            var keys = p.AppKeys(tStopHammer, Probe.VkC);
            r.Expect(keys.Count(e => e.IsKeyDown) == keys.Count(e => e.IsKeyUp), "an anti-AFK key-down has no key-up");

            p.KeyTap(Probe.VkEsc);
            p.Sleep(500);
            r.Expect(!Probe.LeftButtonDown(), "left button down at the end");
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

    /// <summary>The magnifier on the probe window's stripe pattern.</summary>
    public static ScenarioResult Magnifier(Probe p, string exe, Native.POINT centre, Action<bool> showStripes, Func<int> clicksAtWindow)
    {
        var r = new ScenarioResult("C#", "Magnifier (F10): centred, click-through, no focus steal, no recursion");
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

            // Zoom 4.0x through the UI.
            var root = AutomationElement.FromHandle(AppWindow());
            var tab = Find(root, ControlType.TabItem, e => e.Current.Name.Contains("Magnifier"))!;
            ((SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            p.Sleep(300);
            var combo = Find(root, ControlType.ComboBox, e => e.Current.AutomationId == "ZoomBox")!;
            ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
            p.Sleep(300);
            var items = combo.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
                .Cast<AutomationElement>().ToList();
            r.Metric("zoom items", string.Join(", ", items.Select(i => i.Current.Name)));
            var item = items.FirstOrDefault(i => i.Current.Name.StartsWith("4"));
            r.Expect(item is not null, "4.0x not found in the zoom list");
            if (item is null) return r;
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
            p.Sleep(400);
            var zoom4 = Median(Runs(Native.CaptureRow(rowX, centre.Y, rowWidth)));
            r.Metric("stripe width with lens at 4.0x px", F(zoom4));
            r.Expect(Math.Abs(zoom4 - 4 * baseline) <= 4, "4.0x zoom is not applied");
            var status = Find(root, ControlType.Text, e => e.Current.Name.StartsWith("Magnifier:"));
            r.Metric("status line", status?.Current.Name ?? "?");

            // F10 again: gone at once.
            p.Activate();
            var tOff = Recorder.Now;
            p.KeyTap(0x79);
            p.Sleep(150);
            r.Metric("lens visible 150 ms after F10", Native.IsWindowVisible(host));
            r.Expect(!Native.IsWindowVisible(host), "F10 did not remove the lens immediately");
            var after = Median(Runs(Native.CaptureRow(rowX, centre.Y, rowWidth)));
            r.Expect(Math.Abs(after - baseline) <= 2, "screen still magnified after turning it off");

            // On again, then quit: the window must go away with the app.
            p.KeyTap(0x79);
            p.Sleep(400);
            r.Expect(Native.IsWindowVisible(Native.FindWindowW(HostClass, null)), "lens did not come back on");
            Native.PostMessageW(AppWindow(), 0x0010 /* WM_CLOSE */, 0, 0);
            r.Expect(proc.WaitForExit(4000), "app did not exit");
            p.Sleep(300);
            r.Metric("lens window after app exit", Native.FindWindowW(HostClass, null) == 0 ? "gone" : "STILL THERE");
            r.Expect(Native.FindWindowW(HostClass, null) == 0, "lens window left behind after exit");

            var saved = CSharpTarget.ReadSettings();
            r.Metric("saved zoom", saved?["magnifier"]?["zoom"]?.ToString() ?? "?");
            r.Expect(saved?["magnifier"]?["zoom"]?.GetValue<double>() == 4.0, "zoom was not saved");
        }
        finally
        {
            p.Shutdown(proc);
            showStripes(false);
        }
        return r;
    }
}
