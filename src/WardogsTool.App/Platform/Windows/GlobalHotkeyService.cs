using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using WardogsTool.Core.Input;

namespace WardogsTool.App.Platform.Windows;

/// <summary>
/// Global hotkeys through Raw Input (RIDEV_INPUTSINK), observing only.
/// </summary>
/// <remarks>
/// Why Raw Input:
/// <list type="bullet">
/// <item>RegisterHotKey would swallow F8/F9/Esc — the game would no longer see Esc — and fails if
/// another program already owns the key.</item>
/// <item>A low-level keyboard hook is silently removed by Windows if a callback ever exceeds the
/// hook timeout, after which hotkeys stop working for good.</item>
/// <item>Raw Input is event-driven (no polling, unlike the Python tool's 20 ms GetAsyncKeyState
/// loop), has no callback timeout, and leaves every key to reach the game unchanged.</item>
/// </list>
/// Raw Input reports each make, auto-repeat makes and the break; <see cref="HotkeyEdgeDetector"/>
/// turns that into one event per fresh press. Injected input (hDevice = 0) is reported too, which
/// lets the integration probe drive the hotkeys.
/// </remarks>
internal sealed class GlobalHotkeyService : IDisposable
{
    private readonly HashSet<int> _watched;
    private readonly HotkeyEdgeDetector _edges = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private HwndSource? _source;

    public GlobalHotkeyService(IEnumerable<int> virtualKeys)
    {
        _watched = [.. virtualKeys];
    }

    /// <summary>Raised on the window's UI thread for each fresh press of a watched key.</summary>
    public event Action<int>? Pressed;

    /// <summary>Registers for keyboard raw input on <paramref name="window"/>'s handle.</summary>
    public void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("No HwndSource for the window.");
        _source.AddHook(WndProc);

        var device = new NativeMethods.RAWINPUTDEVICE
        {
            UsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
            Usage = NativeMethods.HID_USAGE_GENERIC_KEYBOARD,
            Flags = NativeMethods.RIDEV_INPUTSINK,
            Target = handle,
        };
        if (!NativeMethods.RegisterRawInputDevices([device], 1, (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>()))
            throw new System.ComponentModel.Win32Exception();
        _edges.Reset();
    }

    private unsafe nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_INPUT)
            return 0;

        var headerSize = (uint)sizeof(NativeMethods.RAWINPUTHEADER);
        uint size = 0;
        NativeMethods.GetRawInputData(lParam, NativeMethods.RID_INPUT, null, ref size, headerSize);
        if (size == 0 || size > 1024)
            return 0;
        var buffer = stackalloc byte[(int)size];
        if (NativeMethods.GetRawInputData(lParam, NativeMethods.RID_INPUT, buffer, ref size, headerSize) == unchecked((uint)-1))
            return 0;

        var header = (NativeMethods.RAWINPUTHEADER*)buffer;
        if (header->Type != NativeMethods.RIM_TYPEKEYBOARD)
            return 0;
        var keyboard = (NativeMethods.RAWKEYBOARD*)(buffer + headerSize);
        int vk = keyboard->VKey;
        if (!_watched.Contains(vk))
            return 0;

        var isBreak = (keyboard->Flags & NativeMethods.RI_KEY_BREAK) != 0;
        if (_edges.OnKey(vk, isBreak, _clock.Elapsed))
            Pressed?.Invoke(vk);

        // handled stays false: WPF passes WM_INPUT on to DefWindowProc, which Windows needs to
        // free the input buffer. Nothing is consumed — the key still reaches the game.
        return 0;
    }

    public void Dispose()
    {
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
