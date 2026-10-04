using System.Runtime.InteropServices;

namespace WardogsTool.App.Platform.Windows;

/// <summary>
/// Every Win32 call the app makes. Nothing outside Platform/Windows uses P/Invoke.
/// </summary>
/// <remarks>
/// Input injection deliberately uses the same legacy calls as wardogs_tool.py — mouse_event and
/// keybd_event, with the same arguments — rather than SendInput directly. Both are thin wrappers
/// that Windows turns into SendInput, so the events the game sees are identical to the Python
/// tool's (verified with a low-level hook by tools/WardogsTool.InputProbe).
/// </remarks>
internal static partial class NativeMethods
{
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MAPVK_VK_TO_VSC = 0;

    public const int WM_INPUT = 0x00FF;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIM_TYPEKEYBOARD = 1;
    public const ushort RI_KEY_BREAK = 0x0001;
    public const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    public const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

    public const int ATTACH_PARENT_PROCESS = -1;

    [LibraryImport("user32.dll")]
    public static partial void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);

    [LibraryImport("user32.dll")]
    public static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [LibraryImport("user32.dll")]
    public static partial short VkKeyScanW(ushort ch); // WCHAR

    [LibraryImport("user32.dll")]
    public static partial uint MapVirtualKeyW(uint uCode, uint uMapType);

    public const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
    public const uint TIMER_ALL_ACCESS = 0x001F0003;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial Microsoft.Win32.SafeHandles.SafeWaitHandle CreateWaitableTimerExW(nint attributes, string? name, uint flags, uint access);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWaitableTimer(Microsoft.Win32.SafeHandles.SafeWaitHandle timer, ref long dueTime, int period,
        nint completionRoutine, nint argToCompletionRoutine, [MarshalAs(UnmanagedType.Bool)] bool resume);

    [LibraryImport("winmm.dll")]
    public static partial uint timeBeginPeriod(uint uPeriod);

    [LibraryImport("winmm.dll")]
    public static partial uint timeEndPeriod(uint uPeriod);

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);

    [LibraryImport("user32.dll")]
    public static unsafe partial uint GetRawInputData(nint rawInput, uint command, void* data, ref uint size, uint headerSize);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(int processId);

    // ---------- Magnifier: documented Windows Magnification API + a plain Win32 host window ----------

    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint LWA_ALPHA = 0x00000002;
    public const int SW_HIDE = 0;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOZORDER = 0x0004;
    public static readonly nint HWND_TOPMOST = -1;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MW_FILTERMODE_EXCLUDE = 0;
    public const string WC_MAGNIFIER = "Magnifier";

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct MONITORINFOEXW
    {
        public uint Size;
        public RECT Monitor;
        public RECT Work;
        public uint Flags;
        public fixed char Device[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MAGTRANSFORM
    {
        public fixed float V[9];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSmall;
    }

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagInitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagUninitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagSetWindowSource(nint hwnd, RECT rect);

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool MagSetWindowTransform(nint hwnd, MAGTRANSFORM* transform);

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool MagSetWindowFilterList(nint hwnd, uint filterMode, int count, nint* windows);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial ushort RegisterClassExW(in WNDCLASSEXW windowClass);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterClassW(string className, nint instance);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(nint hwnd, uint colorKey, byte alpha, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InvalidateRect(nint hwnd, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MONITORINFOEXW info);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? moduleName);
}
