using System.Collections.Concurrent;
using System.Diagnostics;

namespace WardogsTool.InputProbe;

/// <summary>One input event seen by the low-level hooks.</summary>
internal readonly record struct InputEvent(long Ticks, bool IsMouse, int Message, uint Vk, uint Scan, uint Flags, nuint Extra, uint MouseData)
{
    public bool Injected => IsMouse ? (Flags & Native.LLMHF_INJECTED) != 0 : (Flags & Native.LLKHF_INJECTED) != 0;
    public bool FromProbe => Extra == Recorder.ProbeMarker;
    /// <summary>Injected by the app under test (both tools pass dwExtraInfo = 0).</summary>
    public bool FromApp => Injected && Extra == 0;
    public bool IsLeftDown => IsMouse && Message == Native.WM_LBUTTONDOWN;
    public bool IsLeftUp => IsMouse && Message == Native.WM_LBUTTONUP;
    public bool IsKeyDown => !IsMouse && (Message == Native.WM_KEYDOWN || Message == Native.WM_SYSKEYDOWN);
    public bool IsKeyUp => !IsMouse && (Message == Native.WM_KEYUP || Message == Native.WM_SYSKEYUP);
    public double Ms => Ticks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>
/// Low-level mouse and keyboard hooks, installed on the probe's UI thread (whose message loop
/// services them). They only observe; every event is passed on.
/// </summary>
internal sealed class Recorder : IDisposable
{
    /// <summary>dwExtraInfo on everything the probe injects, so its events are never mistaken for the app's.</summary>
    public const nuint ProbeMarker = 0x57A7_D065;

    private readonly ConcurrentQueue<InputEvent> _events = new();
    private readonly Native.HookProc _mouseProc;
    private readonly Native.HookProc _keyProc;
    private nint _mouseHook;
    private nint _keyHook;

    public Recorder()
    {
        _mouseProc = MouseProc;
        _keyProc = KeyProc;
    }

    /// <summary>Set when a real (non-injected) mouse movement is seen: someone is using the mouse.</summary>
    public volatile bool UserMovedMouse;

    public void Install()
    {
        var module = Native.GetModuleHandleW(null);
        _mouseHook = Native.SetWindowsHookExW(Native.WH_MOUSE_LL, _mouseProc, module, 0);
        _keyHook = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, _keyProc, module, 0);
        if (_mouseHook == 0 || _keyHook == 0)
            throw new InvalidOperationException("Could not install low-level hooks.");
    }

    public static long Now => Stopwatch.GetTimestamp();

    public static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    public List<InputEvent> Since(long ticks) => _events.Where(e => e.Ticks >= ticks).ToList();

    private unsafe nint MouseProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = (Native.MSLLHOOKSTRUCT*)lParam;
            var msg = (int)wParam;
            if (msg == Native.WM_MOUSEMOVE)
            {
                if ((info->Flags & Native.LLMHF_INJECTED) == 0)
                    UserMovedMouse = true;
            }
            else
            {
                _events.Enqueue(new InputEvent(Now, true, msg, 0, 0, info->Flags, info->ExtraInfo, info->MouseData));
            }
        }
        return Native.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private unsafe nint KeyProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = (Native.KBDLLHOOKSTRUCT*)lParam;
            _events.Enqueue(new InputEvent(Now, false, (int)wParam, info->VkCode, info->ScanCode, info->Flags, info->ExtraInfo, 0));
        }
        return Native.CallNextHookEx(_keyHook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouseHook != 0) Native.UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != 0) Native.UnhookWindowsHookEx(_keyHook);
        _mouseHook = _keyHook = 0;
    }
}
