using System.IO;
using System.Diagnostics;
using System.Windows.Automation;

namespace WardogsTool.InputProbe;

/// <summary>
/// C#-only checks through UI Automation: mortar workflow in the real window, always-on-top,
/// settings persistence, corrupt settings, no elevation.
/// </summary>
internal static class UiScenarios
{
    private static AutomationElement Root(Process p) => AutomationElement.FromHandle(p.MainWindowHandle);

    private static AutomationElement? Find(AutomationElement root, ControlType type, Func<AutomationElement, bool>? where = null, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            var all = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
            foreach (AutomationElement e in all)
                if (where is null || where(e)) return e;
            Thread.Sleep(100);
        } while (sw.ElapsedMilliseconds < timeoutMs);
        return null;
    }

    private static string Name(AutomationElement e) => e.Current.Name ?? "";

    private static AutomationElement? Text(AutomationElement root, string startsWith) =>
        Find(root, ControlType.Text, e => Name(e).StartsWith(startsWith, StringComparison.Ordinal), 500);

    private static bool IsTopmost(Process p) => (Native.GetWindowLongPtrW(p.MainWindowHandle, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0;

    private static bool IsElevated(Process p)
    {
        if (!Native.OpenProcessToken(p.Handle, 0x0008 /* TOKEN_QUERY */, out var token)) return false;
        try
        {
            return Native.GetTokenInformation(token, 20 /* TokenElevation */, out var elevated, 4, out _) && elevated != 0;
        }
        finally
        {
            Native.CloseHandle(token);
        }
    }

    public static List<ScenarioResult> Run(Probe p, string exe)
    {
        var results = new List<ScenarioResult>();
        var target = new CSharpTarget(exe);

        // 1) Mortar workflow, always-on-top toggle, window move; then settings written on close.
        var r = new ScenarioResult("C#", "Mortar workflow in the window (three known cases)");
        var r2 = new ScenarioResult("C#", "Always on top + settings persistence");
        var proc = target.Launch(new TargetSettings(AlwaysOnTop: false, X: 40, Y: 40));
        Native.RECT movedRect = default;
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException("C# window did not appear");
            p.Sleep(1000);
            var root = Root(proc);
            r2.Metric("elevated", IsElevated(proc));
            r2.Expect(!IsElevated(proc), "the app runs elevated");
            r2.Metric("topmost at start (setting false)", IsTopmost(proc));
            r2.Expect(!IsTopmost(proc), "window is topmost although the setting is off");

            var mortarTab = Find(root, ControlType.TabItem, e => Name(e).Contains("Mortar"))!;
            ((SelectionItemPattern)mortarTab.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            p.Sleep(300);
            var boxes = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            var mortarBox = boxes.Cast<AutomationElement>().Single(e => e.Current.AutomationId == "MortarBox");
            var targetBox = boxes.Cast<AutomationElement>().Single(e => e.Current.AutomationId == "TargetBox");
            ((ValuePattern)mortarBox.GetCurrentPattern(ValuePattern.Pattern)).SetValue("100.32 59.45");

            (string Mortar, string Target, string Dir, string Range)[] cases =
            [
                ("100.32 59.45", "104.39 63.59", "DIRECTION: 045°", "RANGE:     581 m"),
                ("78.49 71.84", "81.44, 70.78", "DIRECTION: 110°", "RANGE:     313 m"),
                ("78.49 71.84", "83.60 72.96", "DIRECTION: 078°", "RANGE:     523 m"),
            ];
            foreach (var c in cases)
            {
                ((ValuePattern)mortarBox.GetCurrentPattern(ValuePattern.Pattern)).SetValue(c.Mortar);
                ((ValuePattern)targetBox.GetCurrentPattern(ValuePattern.Pattern)).SetValue(c.Target);
                targetBox.SetFocus();
                p.Sleep(150);
                p.KeyTap(Probe.VkReturn, 30);
                p.Sleep(300);
                var dir = Text(root, "DIRECTION:");
                var range = Text(root, "RANGE:");
                var dirText = dir is null ? "?" : Name(dir);
                var rangeText = range is null ? "?" : Name(range);
                r.Metric($"{c.Mortar} -> {c.Target}", $"{dirText} | {rangeText}");
                r.Expect(dirText == c.Dir && rangeText == c.Range, $"{c.Target}: got '{dirText}' / '{rangeText}'");
                var selection = ((TextPattern)targetBox.GetCurrentPattern(TextPattern.Pattern)).GetSelection();
                var selected = selection.Length > 0 ? selection[0].GetText(-1) : "";
                r.Expect(selected == c.Target, $"target text not selected after Enter (selected '{selected}')");
                r.Expect(((ValuePattern)mortarBox.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == c.Mortar, "mortar position was not kept");
            }
            var history = Find(root, ControlType.List)!;
            r.Metric("history rows", history.FindAll(TreeScope.Children, Condition.TrueCondition).Count);

            // Errors keep the last result and show a red message.
            ((ValuePattern)targetBox.GetCurrentPattern(ValuePattern.Pattern)).SetValue("abc 1");
            targetBox.SetFocus();
            p.Sleep(150);
            p.KeyTap(Probe.VkReturn, 30);
            p.Sleep(300);
            var err = Text(root, "目标坐标:");
            r.Metric("error line", err is null ? "?" : Name(err));
            r.Expect(err is not null && Name(err) == "目标坐标: could not convert string to float: 'abc'", "error message differs from Python");
            r.Expect(Text(root, "DIRECTION: 078°") is not null, "previous result was cleared by an error");

            var topmost = Find(root, ControlType.CheckBox)!;
            ((TogglePattern)topmost.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            p.Sleep(300);
            r2.Metric("topmost after ticking the box", IsTopmost(proc));
            r2.Expect(IsTopmost(proc), "ticking 窗口置顶 did not make the window topmost");

            Native.SetWindowPos(proc.MainWindowHandle, 0, 150, 120, 0, 0, 0x0001 | 0x0004 | 0x0010 /* NOSIZE|NOZORDER|NOACTIVATE */);
            p.Sleep(300);
            Native.GetWindowRect(proc.MainWindowHandle, out movedRect);
            proc.CloseMainWindow();
            r2.Expect(proc.WaitForExit(4000), "app did not exit on close");
        }
        finally
        {
            p.Shutdown(proc);
        }

        var saved = CSharpTarget.ReadSettings();
        r2.Metric("saved settings", saved?.ToJsonString() ?? "(none)");
        r2.Expect(saved?["window"]?["alwaysOnTop"]?.GetValue<bool>() == true, "alwaysOnTop was not saved");
        r2.Expect(saved?["mortar"]?["position"]?.GetValue<string>() == "78.49 71.84", "mortar position was not saved");
        r2.Expect(saved?["window"]?["selectedTab"]?.GetValue<int>() == 2, "selected tab was not saved");
        r2.Expect(saved?["hammer"]?["holdMs"]?.GetValue<int>() == 310, "hammer preset was not saved");

        // 2) Relaunch with exactly what was saved: topmost from the start, same place, mortar tab.
        proc = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException("C# window did not appear on relaunch");
            p.Sleep(1000);
            Native.GetWindowRect(proc.MainWindowHandle, out var rect);
            r2.Metric("topmost after relaunch", IsTopmost(proc));
            r2.Metric("window position before close / after relaunch", $"({movedRect.Left},{movedRect.Top}) / ({rect.Left},{rect.Top})");
            r2.Expect(IsTopmost(proc), "topmost was not restored");
            r2.Expect(Math.Abs(rect.Left - movedRect.Left) <= 2 && Math.Abs(rect.Top - movedRect.Top) <= 2, "window position was not restored");
            var root = Root(proc);
            var mortarBox = Find(root, ControlType.Edit, e => e.Current.AutomationId == "MortarBox");
            var value = mortarBox is null ? "" : ((ValuePattern)mortarBox.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            r2.Metric("mortar position after relaunch", value);
            r2.Expect(mortarBox is not null && value == "78.49 71.84", "mortar position / tab not restored");
        }
        finally
        {
            p.Shutdown(proc);
        }
        results.Add(r);
        results.Add(r2);

        // 3) Corrupt settings must not stop the app from starting.
        var r3 = new ScenarioResult("C#", "Corrupt settings.json does not block startup");
        File.WriteAllText(CSharpTarget.SettingsFile, "{ not json");
        proc = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
        try
        {
            var up = Probe.WaitForWindow(proc, 15000);
            p.Sleep(1500);
            r3.Metric("window appeared", up);
            r3.Metric("settings.json.bad kept", File.Exists(CSharpTarget.SettingsFile + ".bad"));
            r3.Expect(up && !proc.HasExited, "app did not start");
            r3.Expect(File.Exists(CSharpTarget.SettingsFile + ".bad"), "corrupt file was not kept aside");
            // Dismiss the warning dialog.
            foreach (AutomationElement w in AutomationElement.RootElement.FindAll(TreeScope.Children,
                         new PropertyCondition(AutomationElement.ProcessIdProperty, proc.Id)))
            {
                var ok = w.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                if (ok is not null && w.Current.ClassName == "#32770")
                    ((InvokePattern)ok.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            }
            p.Sleep(300);
        }
        finally
        {
            p.Shutdown(proc);
        }
        results.Add(r3);
        return results;
    }
}
