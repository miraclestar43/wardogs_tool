using System.Runtime.InteropServices;
using System.Windows.Threading;
using WardogsTool.App.Services;
using WardogsTool.Core.Magnifier;

namespace WardogsTool.App.Platform.Windows;

/// <summary>
/// Centre-screen lens built on the documented Windows Magnification API (Magnification.dll,
/// WC_MAGNIFIER). It only enlarges pixels already on the desktop; it never touches the game
/// process, reads game state or analyses the image.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Host window: borderless popup, WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT
/// (click-through) | WS_EX_NOACTIVATE (never takes focus) | WS_EX_TOOLWINDOW (no taskbar entry).</item>
/// <item>The magnifier control renders through the desktop compositor, so there is no screenshot
/// polling; a ~60 Hz timer only asks it to redraw.</item>
/// <item>The host window is put on the control's exclude list so the lens never magnifies itself.</item>
/// <item>The lens is centred on the monitor that holds the foreground (game) window at the moment
/// it is turned on — primary monitor otherwise — in physical pixels (the app is per-monitor DPI aware).</item>
/// </list>
/// Must be created and used on the UI thread (MagInitialize is per thread).
/// </remarks>
internal sealed unsafe class MagnifierOverlay : IMagnifierOverlay
{
    private const string HostClass = "WardogsToolMagnifierHost";

    private readonly DispatcherTimer _refresh;
    private bool _magInitialized;
    private nint _classNamePtr;
    private nint _host;
    private nint _mag;
    private PixelRect? _monitor;

    public MagnifierOverlay(Dispatcher dispatcher)
    {
        _refresh = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            if (_mag != 0)
                NativeMethods.InvalidateRect(_mag, 0, false);
        }, dispatcher);
        _refresh.Stop();
    }

    public bool IsShown { get; private set; }

    public string Display { get; private set; } = "";

    public MagnifierLayout? Layout { get; private set; }

    public void Show(double zoom)
    {
        EnsureCreated();
        var (monitor, name) = TargetMonitor();
        _monitor = monitor;
        var layout = MagnifierGeometry.Compute(monitor, zoom);
        Display = $"{name} · {monitor.Width}×{monitor.Height}";
        Apply(layout);
        NativeMethods.SetWindowPos(_host, NativeMethods.HWND_TOPMOST, layout.Lens.Left, layout.Lens.Top,
            layout.Lens.Width, layout.Lens.Height, NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        IsShown = true;
        _refresh.Start();
    }

    /// <summary>
    /// Changes the zoom while shown: same monitor and lens, new source area. Also puts the lens
    /// back on top, in case another topmost window (e.g. a topmost game window that was clicked)
    /// has been raised above it since it was shown.
    /// </summary>
    public void SetZoom(double zoom)
    {
        if (!IsShown || _monitor is not { } monitor)
            return;
        var layout = MagnifierGeometry.Compute(monitor, zoom);
        Apply(layout);
        NativeMethods.SetWindowPos(_host, NativeMethods.HWND_TOPMOST, layout.Lens.Left, layout.Lens.Top,
            layout.Lens.Width, layout.Lens.Height, NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    public void Hide()
    {
        _refresh.Stop();
        if (_host != 0)
            NativeMethods.ShowWindow(_host, NativeMethods.SW_HIDE);
        IsShown = false;
    }

    public void Dispose()
    {
        Hide();
        if (_host != 0)
        {
            NativeMethods.DestroyWindow(_host); // also destroys the magnifier child
            _host = _mag = 0;
        }
        if (_classNamePtr != 0)
        {
            NativeMethods.UnregisterClassW(HostClass, NativeMethods.GetModuleHandleW(null));
            Marshal.FreeHGlobal(_classNamePtr);
            _classNamePtr = 0;
        }
        if (_magInitialized)
        {
            NativeMethods.MagUninitialize();
            _magInitialized = false;
        }
    }

    private void Apply(MagnifierLayout layout)
    {
        NativeMethods.SetWindowPos(_mag, 0, 0, 0, layout.Lens.Width, layout.Lens.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
        var transform = new NativeMethods.MAGTRANSFORM();
        transform.V[0] = (float)layout.Zoom;
        transform.V[4] = (float)layout.Zoom;
        transform.V[8] = 1f;
        if (!NativeMethods.MagSetWindowTransform(_mag, &transform))
            throw new InvalidOperationException("MagSetWindowTransform failed.");
        var source = new NativeMethods.RECT { Left = layout.Source.Left, Top = layout.Source.Top, Right = layout.Source.Right, Bottom = layout.Source.Bottom };
        if (!NativeMethods.MagSetWindowSource(_mag, source))
            throw new InvalidOperationException("MagSetWindowSource failed.");
        NativeMethods.InvalidateRect(_mag, 0, false);
        Layout = layout;
    }

    private void EnsureCreated()
    {
        if (_host != 0)
            return;
        if (!_magInitialized)
        {
            if (!NativeMethods.MagInitialize())
                throw new InvalidOperationException("Windows Magnification API is not available (MagInitialize failed).");
            _magInitialized = true;
        }

        var instance = NativeMethods.GetModuleHandleW(null);
        if (_classNamePtr == 0)
        {
            _classNamePtr = Marshal.StringToHGlobalUni(HostClass);
            var wc = new NativeMethods.WNDCLASSEXW
            {
                Size = (uint)sizeof(NativeMethods.WNDCLASSEXW),
                // .NET Framework has no [UnmanagedCallersOnly]; a delegate kept alive in a static field instead.
                WndProc = Marshal.GetFunctionPointerForDelegate(HostWndProcDelegate),
                Instance = instance,
                ClassName = _classNamePtr,
            };
            if (NativeMethods.RegisterClassExW(wc) == 0 && Marshal.GetLastWin32Error() != 1410 /* already registered */)
                throw new System.ComponentModel.Win32Exception();
        }

        _host = NativeMethods.CreateWindowExW(
            NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT
                | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW,
            HostClass, "WardogsTool Magnifier", NativeMethods.WS_POPUP | NativeMethods.WS_CLIPCHILDREN,
            0, 0, MagnifierGeometry.DefaultLensWidth, MagnifierGeometry.DefaultLensHeight, 0, 0, instance, 0);
        if (_host == 0)
            throw new System.ComponentModel.Win32Exception();
        NativeMethods.SetLayeredWindowAttributes(_host, 0, 255, NativeMethods.LWA_ALPHA);

        // No MS_SHOWMAGNIFIEDCURSOR: nothing is drawn over the image (no fake crosshair, no cursor).
        _mag = NativeMethods.CreateWindowExW(0, NativeMethods.WC_MAGNIFIER, "WardogsTool Lens",
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE,
            0, 0, MagnifierGeometry.DefaultLensWidth, MagnifierGeometry.DefaultLensHeight, _host, 0, instance, 0);
        if (_mag == 0)
            throw new System.ComponentModel.Win32Exception();

        var exclude = _host;
        NativeMethods.MagSetWindowFilterList(_mag, NativeMethods.MW_FILTERMODE_EXCLUDE, 1, &exclude);
    }

    // Ignore WM_CLOSE: only the app hides/destroys the lens (anything that closes "the process's
    // main window" from outside could otherwise pick this top-level window and leave a dangling handle).
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    // Static so the GC can never collect it while Windows still holds the function pointer.
    private static readonly WndProcDelegate HostWndProcDelegate = HostWndProc;

    private static nint HostWndProc(nint hwnd, uint msg, nint wParam, nint lParam) =>
        msg == 0x0010 /* WM_CLOSE */ ? 0 : NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);

    private static (PixelRect Monitor, string Name) TargetMonitor()
    {
        var monitor = NativeMethods.MonitorFromWindow(NativeMethods.GetForegroundWindow(), NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        return Describe(monitor);
    }

    private static (PixelRect Monitor, string Name) Describe(nint monitor)
    {
        var info = new NativeMethods.MONITORINFOEXW { Size = (uint)sizeof(NativeMethods.MONITORINFOEXW) };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException("GetMonitorInfo failed.");
        var name = new string(info.Device).TrimEnd('\0');
        var r = info.Monitor;
        return (new PixelRect(r.Left, r.Top, r.Right, r.Bottom), name);
    }
}
