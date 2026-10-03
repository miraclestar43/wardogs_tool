using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace WardogsTool.InputProbe;

internal sealed class AbortException(string message) : Exception(message);

internal sealed class ScenarioResult(string target, string name)
{
    public string Target { get; } = target;
    public string Name { get; } = name;
    public bool Pass { get; set; } = true;
    public List<(string Key, string Value)> Metrics { get; } = [];
    public List<string> Failures { get; } = [];

    public void Metric(string key, object value) => Metrics.Add((key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""));

    public void Expect(bool condition, string what)
    {
        if (!condition)
        {
            Pass = false;
            Failures.Add(what);
        }
    }
}

/// <summary>Shared plumbing: injection with the probe marker, waiting on hook events, safety.</summary>
internal sealed class Probe(Recorder recorder, nint probeWindow, Native.POINT centre, ConcurrentQueue<(long Ticks, int Vk)> keysAtWindow)
{
    public const int VkF8 = 0x77, VkF9 = 0x78, VkEsc = 0x1B, VkF12 = 0x7B, VkC = 0x43, VkReturn = 0x0D;

    public Recorder Recorder { get; } = recorder;

    public static long Ms(double ms) => (long)(ms * Stopwatch.Frequency / 1000);

    public void CheckSafe()
    {
        if (Recorder.UserMovedMouse)
            throw new AbortException("the mouse was moved by hand during the run");
    }

    public void Sleep(int ms)
    {
        var end = Recorder.Now + Ms(ms);
        while (Recorder.Now < end)
        {
            CheckSafe();
            Thread.Sleep(Math.Min(5, Math.Max(1, (int)Recorder.ToMs(end - Recorder.Now))));
        }
    }

    public void SleepUntil(long ticks)
    {
        var left = Recorder.ToMs(ticks - Recorder.Now);
        if (left > 0) Sleep((int)Math.Ceiling(left));
    }

    private static byte Scan(int vk) => (byte)Native.MapVirtualKeyW((uint)vk, 0);

    public long KeyMake(int vk)
    {
        var t = Recorder.Now;
        Native.keybd_event((byte)vk, Scan(vk), 0, Recorder.ProbeMarker);
        return t;
    }

    public void KeyBreak(int vk) => Native.keybd_event((byte)vk, Scan(vk), Native.KEYEVENTF_KEYUP, Recorder.ProbeMarker);

    /// <summary>A key press long enough for the Python tool's 20 ms GetAsyncKeyState poll to see.</summary>
    public long KeyTap(int vk, int holdMs = 60)
    {
        var t = KeyMake(vk);
        Thread.Sleep(holdMs);
        KeyBreak(vk);
        return t;
    }

    /// <summary>Puts the cursor in the middle of the probe window and clicks it so it has focus.</summary>
    public void Activate()
    {
        CheckSafe();
        Native.SetCursorPos(centre.X, centre.Y);
        Thread.Sleep(50);
        var under = Native.GetAncestor(Native.WindowFromPoint(centre), 2 /* GA_ROOT */);
        if (under != probeWindow)
            throw new AbortException("the probe window is not under the cursor; clicks would land elsewhere");
        Native.mouse_event(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, Recorder.ProbeMarker);
        Thread.Sleep(30);
        Native.mouse_event(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, Recorder.ProbeMarker);
        Thread.Sleep(200);
        Recorder.UserMovedMouse = false;
    }

    public InputEvent? WaitFor(Func<InputEvent, bool> match, long since, int timeoutMs)
    {
        var end = Recorder.Now + Ms(timeoutMs);
        while (Recorder.Now < end)
        {
            CheckSafe();
            foreach (var e in Recorder.Since(since))
                if (match(e)) return e;
            Thread.Sleep(1);
        }
        return null;
    }

    public List<InputEvent> AppMouse(long since) => Recorder.Since(since).Where(e => e.FromApp && (e.IsLeftDown || e.IsLeftUp)).ToList();

    public List<InputEvent> AppKeys(long since, int vk) => Recorder.Since(since).Where(e => e.FromApp && !e.IsMouse && e.Vk == vk).ToList();

    public static bool LeftButtonDown() => (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0;

    /// <summary>True if the probe window received a key-down for vk after the given time (= not swallowed).</summary>
    public bool KeyReachedWindow(int vk, long since) =>
        keysAtWindow.Any(k => k.Vk == vk && k.Ticks >= since && k.Ticks <= since + Ms(1000));

    public static bool WaitForWindow(Process p, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            p.Refresh();
            if (p.HasExited) return false;
            if (p.MainWindowHandle != 0) return true;
            Thread.Sleep(50);
        }
        return false;
    }

    /// <summary>Stops and closes a target without leaving anything pressed.</summary>
    public void Shutdown(Process p)
    {
        if (p.HasExited) return;
        KeyTap(VkEsc);
        p.CloseMainWindow();
        if (!p.WaitForExit(4000))
        {
            p.Kill();
            p.WaitForExit(2000);
        }
    }
}

internal static class Scenarios
{
    private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    private static (List<double> Holds, List<double> Gaps) HammerIntervals(List<InputEvent> evs, bool lastHoldInterrupted)
    {
        var holds = new List<double>();
        var gaps = new List<double>();
        for (var i = 0; i + 1 < evs.Count; i++)
        {
            if (evs[i].IsLeftDown && evs[i + 1].IsLeftUp) holds.Add(evs[i + 1].Ms - evs[i].Ms);
            if (evs[i].IsLeftUp && evs[i + 1].IsLeftDown) gaps.Add(evs[i + 1].Ms - evs[i].Ms);
        }
        if (lastHoldInterrupted && holds.Count > 0) holds.RemoveAt(holds.Count - 1);
        return (holds, gaps);
    }

    private static void Stats(ScenarioResult r, string label, List<double> xs, double nominal, double meanTol, double maxTol)
    {
        if (xs.Count == 0)
        {
            r.Expect(false, $"no {label} intervals recorded");
            return;
        }
        var mean = xs.Average();
        r.Metric($"{label} n", xs.Count);
        r.Metric($"{label} mean/min/max ms", $"{F(mean)} / {F(xs.Min())} / {F(xs.Max())}");
        r.Expect(Math.Abs(mean - nominal) <= meanTol, $"{label} mean {F(mean)} ms is not within {meanTol} ms of {nominal}");
        r.Expect(xs.All(x => Math.Abs(x - nominal) <= maxTol), $"a {label} interval is more than {maxTol} ms from {nominal}");
    }

    private static void EventFields(ScenarioResult r, List<InputEvent> evs, string label)
    {
        var shapes = evs.Select(e => e.IsMouse
                ? $"{(e.IsLeftDown ? "LBUTTONDOWN" : "LBUTTONUP")} flags=0x{e.Flags:X} mouseData=0x{e.MouseData:X} extra=0x{(ulong)e.Extra:X}"
                : $"{(e.IsKeyDown ? "KEYDOWN" : "KEYUP")} vk=0x{e.Vk:X2} scan=0x{e.Scan:X2} flags=0x{e.Flags:X} extra=0x{(ulong)e.Extra:X}")
            .Distinct().OrderBy(s => s);
        r.Metric($"{label} event shapes", string.Join("; ", shapes));
    }

    /// <summary>Hammer at the launched hold; Esc lands 150 ms into a hold.</summary>
    public static ScenarioResult HammerStopDuringHold(Probe p, string target, int holdMs)
    {
        var r = new ScenarioResult(target, $"Hammer {holdMs} ms, stop during hold");
        p.Activate();
        var t0 = p.KeyTap(Probe.VkF9);
        p.Sleep(2600);
        var d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
        r.Expect(d is not null, "no mouse-down after F9");
        if (d is null) return r;
        p.SleepUntil(d.Value.Ticks + Probe.Ms(150));
        var tEsc = p.KeyTap(Probe.VkEsc);
        p.Sleep(800);

        var evs = p.AppMouse(t0);
        var (holds, gaps) = HammerIntervals(evs, lastHoldInterrupted: true);
        var lastUp = evs.LastOrDefault(e => e.IsLeftUp);
        r.Metric("first mouse-down after F9 ms", F(evs[0].Ms - Recorder.ToMs(t0)));
        Stats(r, "hold", holds, holdMs, 3, 15);
        Stats(r, "gap", gaps, 40, 3, 15);
        r.Metric("mouse-up after Esc ms", F(lastUp.Ms - Recorder.ToMs(tEsc)));
        r.Metric("downs / ups", $"{evs.Count(e => e.IsLeftDown)} / {evs.Count(e => e.IsLeftUp)}");
        EventFields(r, evs, "mouse");
        r.Expect(evs[0].IsLeftDown, "first event is not a mouse-down");
        r.Expect(evs.Count(e => e.IsLeftDown) == evs.Count(e => e.IsLeftUp), "downs and ups do not match");
        r.Expect(evs[^1].IsLeftUp, "last event is not a mouse-up");
        r.Expect(lastUp.Ticks > tEsc && Recorder.ToMs(lastUp.Ticks - tEsc) < 60, "the stop did not release the button promptly");
        r.Expect(!Probe.LeftButtonDown(), "left button still reported down after stop");
        r.Expect(p.KeyReachedWindow(Probe.VkF9, t0) && p.KeyReachedWindow(Probe.VkEsc, tEsc), "F9/Esc did not reach the focused window (swallowed?)");
        return r;
    }

    /// <summary>Esc lands 10 ms into the 40 ms gap: nothing more may be sent.</summary>
    public static ScenarioResult HammerStopDuringGap(Probe p, string target)
    {
        var r = new ScenarioResult(target, "Hammer, stop during gap");
        p.Activate();
        var t0 = p.KeyTap(Probe.VkF9);
        p.Sleep(1200);
        var u = p.WaitFor(e => e.FromApp && e.IsLeftUp, Recorder.Now, 2000);
        r.Expect(u is not null, "no mouse-up seen");
        if (u is null) return r;
        p.SleepUntil(u.Value.Ticks + Probe.Ms(10));
        var tEsc = p.KeyTap(Probe.VkEsc);
        p.Sleep(800);

        var evs = p.AppMouse(t0);
        var after = evs.Where(e => e.Ticks > tEsc).ToList();
        r.Metric("events after Esc", after.Count);
        r.Metric("downs / ups", $"{evs.Count(e => e.IsLeftDown)} / {evs.Count(e => e.IsLeftUp)}");
        // Python polls Esc every 20 ms, so a down 30 ms after Esc can still sneak in before it notices;
        // that down must then be released. Either way the stream must end balanced on an up.
        r.Expect(evs.Count(e => e.IsLeftDown) == evs.Count(e => e.IsLeftUp), "downs and ups do not match");
        r.Expect(evs[^1].IsLeftUp, "last event is not a mouse-up");
        r.Expect(!Probe.LeftButtonDown(), "left button still reported down after stop");
        return r;
    }

    /// <summary>
    /// Anti-AFK with period 3 s: F8, then Esc while the 3rd press (first of round 2) is held.
    /// Expect presses at ~3.0 s, ~3.5 s, ~6.0 s, each held 50 ms, the 3rd key-up after Esc, no 4th press.
    /// </summary>
    public static ScenarioResult AntiAfk(Probe p, string target)
    {
        var r = new ScenarioResult(target, "Anti-AFK c ×2 / 500 ms / 3 s, stop while a key is held");
        p.Activate();
        var t0 = p.KeyTap(Probe.VkF8);
        var since = t0;
        InputEvent? third = null;
        for (var n = 0; n < 3; n++)
        {
            third = p.WaitFor(e => e.FromApp && e.IsKeyDown && e.Vk == Probe.VkC && e.Ticks >= since, since, 9000);
            if (third is null) break;
            since = third.Value.Ticks + 1;
        }
        r.Expect(third is not null, "fewer than 3 anti-AFK presses within 9 s");
        if (third is null) return r;
        var tEsc = p.KeyTap(Probe.VkEsc, 20);
        p.Sleep(1500);

        var evs = p.AppKeys(t0, Probe.VkC);
        var downs = evs.Where(e => e.IsKeyDown).ToList();
        var ups = evs.Where(e => e.IsKeyUp).ToList();
        var baseMs = Recorder.ToMs(t0);
        r.Metric("press times after F8 ms", string.Join(", ", downs.Select(d => F(d.Ms - baseMs))));
        r.Metric("key held ms", string.Join(", ", downs.Zip(ups, (d, u) => F(u.Ms - d.Ms))));
        r.Metric("2nd press after 1st ms", F(downs[1].Ms - downs[0].Ms));
        r.Metric("round 2 after round 1 ms", F(downs[2].Ms - downs[0].Ms));
        EventFields(r, evs, "key");
        r.Expect(downs.Count == 3 && ups.Count == 3, $"expected 3 downs/3 ups, got {downs.Count}/{ups.Count}");
        r.Expect(Math.Abs(downs[0].Ms - baseMs - 3000) < 60, "first press not ~3 s after F8");
        r.Expect(Math.Abs(downs[1].Ms - downs[0].Ms - 500) < 30, "second press not ~500 ms after the first");
        r.Expect(Math.Abs(downs[2].Ms - downs[0].Ms - 3000) < 60, "round 2 not ~3 s after round 1");
        r.Expect(downs.Zip(ups, (d, u) => u.Ms - d.Ms).All(h => h is > 45 and < 75), "a key was not held ~50 ms");
        r.Expect(ups.Count == 3 && ups[2].Ticks > tEsc, "the key held at Esc was not released after the stop");
        r.Expect(p.KeyReachedWindow(Probe.VkC, downs[0].Ticks), "the anti-AFK key did not reach the focused window");
        r.Expect(p.KeyReachedWindow(Probe.VkF8, t0), "F8 did not reach the focused window (swallowed?)");
        return r;
    }

    public static ScenarioResult Simultaneous(Probe p, string target)
    {
        var r = new ScenarioResult(target, "Hammer + anti-AFK together, one Esc stops both");
        p.Activate();
        var t0 = p.KeyTap(Probe.VkF8);
        p.Sleep(200);
        p.KeyTap(Probe.VkF9);
        var c = p.WaitFor(e => e.FromApp && e.IsKeyDown && e.Vk == Probe.VkC, t0, 5000);
        r.Expect(c is not null, "no anti-AFK press while hammering");
        if (c is null) return r;
        p.Sleep(800);
        var tEsc = p.KeyTap(Probe.VkEsc);
        p.Sleep(800);

        var mouse = p.AppMouse(t0);
        var keys = p.AppKeys(t0, Probe.VkC);
        r.Metric("mouse downs before / after first c", $"{mouse.Count(e => e.IsLeftDown && e.Ticks < c.Value.Ticks)} / {mouse.Count(e => e.IsLeftDown && e.Ticks > c.Value.Ticks)}");
        r.Metric("c downs / ups", $"{keys.Count(e => e.IsKeyDown)} / {keys.Count(e => e.IsKeyUp)}");
        var late = mouse.Concat(keys).Where(e => e.Ticks > tEsc + Probe.Ms(120)).ToList();
        r.Metric("events later than Esc + 120 ms", late.Count);
        r.Expect(mouse.Any(e => e.Ticks > c.Value.Ticks), "hammering did not continue during anti-AFK");
        r.Expect(keys.Count(e => e.IsKeyDown) == 2 && keys.Count(e => e.IsKeyUp) == 2, "expected 2 anti-AFK presses");
        r.Expect(mouse.Count(e => e.IsLeftDown) == mouse.Count(e => e.IsLeftUp), "mouse downs and ups do not match");
        r.Expect(late.Count == 0, "something was still sent after Esc");
        r.Expect(!Probe.LeftButtonDown(), "left button still reported down");
        return r;
    }

    /// <summary>
    /// F9 held down with auto-repeat; Esc pressed while it is still held; more repeats. Only a
    /// fresh F9 press may restart hammering.
    /// </summary>
    public static ScenarioResult AutoRepeat(Probe p, string target)
    {
        var r = new ScenarioResult(target, "F9 auto-repeat is not a new press");
        p.Activate();
        var t0 = p.KeyMake(Probe.VkF9);
        for (var i = 0; i < 20; i++) { p.Sleep(33); p.KeyMake(Probe.VkF9); }
        var tEsc = p.KeyTap(Probe.VkEsc);
        for (var i = 0; i < 20; i++) { p.Sleep(33); p.KeyMake(Probe.VkF9); }
        p.KeyBreak(Probe.VkF9);
        p.Sleep(500);
        var afterEsc = p.AppMouse(tEsc + Probe.Ms(120)).Count(e => e.IsLeftDown);
        r.Metric("mouse downs while F9 held, before Esc", p.AppMouse(t0).Count(e => e.IsLeftDown && e.Ticks < tEsc));
        r.Metric("mouse downs after Esc while F9 still repeating", afterEsc);
        r.Expect(p.AppMouse(t0).Any(e => e.IsLeftDown && e.Ticks < tEsc), "the first F9 make did not start hammering");
        r.Expect(afterEsc == 0, "an auto-repeat F9 restarted hammering");

        var t1 = p.KeyTap(Probe.VkF9);
        var restarted = p.WaitFor(e => e.FromApp && e.IsLeftDown, t1, 1000);
        p.Sleep(400);
        p.KeyTap(Probe.VkEsc);
        p.Sleep(500);
        r.Expect(restarted is not null, "a fresh F9 press did not restart hammering");
        r.Expect(!Probe.LeftButtonDown(), "left button still reported down");
        return r;
    }

    /// <summary>Quit while the button is held: F12 or closing the window.</summary>
    public static ScenarioResult QuitDuringHold(Probe p, string target, Process process, bool viaF12)
    {
        var r = new ScenarioResult(target, viaF12 ? "F12 during hold releases and exits" : "Closing the window during hold releases and exits");
        p.Activate();
        var t0 = p.KeyTap(Probe.VkF9);
        p.Sleep(700);
        var d = p.WaitFor(e => e.FromApp && e.IsLeftDown, Recorder.Now, 2000);
        r.Expect(d is not null, "no mouse-down");
        if (d is null) return r;
        p.SleepUntil(d.Value.Ticks + Probe.Ms(100));
        var tQuit = Recorder.Now;
        if (viaF12) p.KeyTap(Probe.VkF12);
        else process.CloseMainWindow();
        var exited = process.WaitForExit(4000);
        p.Sleep(300);

        var evs = p.AppMouse(t0);
        var up = evs.LastOrDefault();
        r.Metric("process exited", exited);
        r.Metric("mouse-up after quit ms", up.IsLeftUp ? F(up.Ms - Recorder.ToMs(tQuit)) : "none");
        r.Expect(exited, "the process did not exit");
        r.Expect(evs.Count > 0 && evs[^1].IsLeftUp && evs[^1].Ticks > tQuit, "no mouse-up after quitting");
        r.Expect(evs.Count(e => e.IsLeftDown) == evs.Count(e => e.IsLeftUp), "downs and ups do not match");
        r.Expect(!Probe.LeftButtonDown(), "left button still reported down after exit");
        return r;
    }

    /// <summary>Runs the shared input scenarios against one tool.</summary>
    public static List<ScenarioResult> RunInputSuite(Probe p, ITarget target)
    {
        var results = new List<ScenarioResult>();

        var proc = target.Launch(new TargetSettings(HoldMs: 310, PeriodSeconds: "3"));
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException($"{target.Name} window did not appear");
            p.Sleep(1200);
            results.Add(HammerStopDuringHold(p, target.Name, 310));
            results.Add(HammerStopDuringGap(p, target.Name));
            results.Add(AntiAfk(p, target.Name));
            results.Add(Simultaneous(p, target.Name));
            results.Add(AutoRepeat(p, target.Name));
            results.Add(QuitDuringHold(p, target.Name, proc, viaF12: true));
        }
        finally
        {
            p.Shutdown(proc);
        }

        proc = target.Launch(new TargetSettings(HoldMs: 510));
        try
        {
            if (!Probe.WaitForWindow(proc, 15000)) throw new AbortException($"{target.Name} window did not appear");
            p.Sleep(1200);
            results.Add(HammerStopDuringHold(p, target.Name, 510));
            results.Add(QuitDuringHold(p, target.Name, proc, viaF12: false));
        }
        finally
        {
            p.Shutdown(proc);
        }

        if (target is PythonTarget python)
        {
            // Tk prints callback exceptions to stderr instead of failing; surface them.
            var r = new ScenarioResult(target.Name, "No errors printed by the tool (stderr)");
            r.Metric("stderr lines", python.Stderr.Count);
            r.Expect(python.Stderr.Count == 0, "stderr: " + string.Join(" / ", python.Stderr.Take(5)));
            results.Add(r);
        }
        return results;
    }
}
