using WardogsTool.Core.Input;

namespace WardogsTool.App.Platform.Windows;

/// <summary>Left button via user32 mouse_event — the call wardogs_tool.py makes.</summary>
internal sealed class WindowsMouseInput : IMouseInput
{
    // Python: user32.mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    public void LeftDown() => NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);

    public void LeftUp() => NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
}

/// <summary>Keys via user32 keybd_event — the call wardogs_tool.py makes.</summary>
internal sealed class WindowsKeyboardInput : IKeyboardInput
{
    // Python char_to_vk: res = VkKeyScanW(ord(ch)); None if res == -1 else res & 0xFF
    public bool TryGetVirtualKey(char c, out byte virtualKey)
    {
        var res = NativeMethods.VkKeyScanW(c);
        virtualKey = (byte)(res & 0xFF);
        return res != -1;
    }

    // Python tap: scan = MapVirtualKeyW(vk, 0)  (ctypes passes it to keybd_event's BYTE parameter)
    public byte GetScanCode(byte virtualKey) =>
        (byte)NativeMethods.MapVirtualKeyW(virtualKey, NativeMethods.MAPVK_VK_TO_VSC);

    // Python: keybd_event(vk, scan, 0, 0)
    public void KeyDown(byte virtualKey, byte scanCode) =>
        NativeMethods.keybd_event(virtualKey, scanCode, 0, 0);

    // Python: keybd_event(vk, scan, KEYEVENTF_KEYUP, 0)
    public void KeyUp(byte virtualKey, byte scanCode) =>
        NativeMethods.keybd_event(virtualKey, scanCode, NativeMethods.KEYEVENTF_KEYUP, 0);
}

/// <summary>
/// Raises the system timer resolution to 1 ms for the app's lifetime, as wardogs_tool.py does
/// (winmm.timeBeginPeriod(1)). The hammer and anti-AFK waits do not depend on it
/// (<see cref="HighResolutionTimeSource"/>); it is kept for parity with the Python process.
/// </summary>
internal sealed class TimerResolution : IDisposable
{
    private bool _active;

    private TimerResolution() => _active = NativeMethods.timeBeginPeriod(1) == 0;

    public static TimerResolution Begin() => new();

    public void Dispose()
    {
        if (_active)
        {
            NativeMethods.timeEndPeriod(1);
            _active = false;
        }
    }
}
