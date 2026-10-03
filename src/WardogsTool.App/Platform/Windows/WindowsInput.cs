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

    // Python tap: scan = MapVirtualKeyW(vk, 0); keybd_event(vk, scan, 0, 0)
    public void KeyDown(byte virtualKey) =>
        NativeMethods.keybd_event(virtualKey, ScanCode(virtualKey), 0, 0);

    // Python: keybd_event(vk, scan, KEYEVENTF_KEYUP, 0)
    public void KeyUp(byte virtualKey) =>
        NativeMethods.keybd_event(virtualKey, ScanCode(virtualKey), NativeMethods.KEYEVENTF_KEYUP, 0);

    private static byte ScanCode(byte virtualKey) =>
        (byte)NativeMethods.MapVirtualKeyW(virtualKey, NativeMethods.MAPVK_VK_TO_VSC);
}

/// <summary>
/// Raises the system timer resolution to 1 ms for the app's lifetime, as wardogs_tool.py does
/// (winmm.timeBeginPeriod(1)). Without it the 40 ms hammer gap stretches to ~46 ms.
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
