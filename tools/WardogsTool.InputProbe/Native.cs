using System.Runtime.InteropServices;

namespace WardogsTool.InputProbe;

internal static partial class Native
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const uint LLMHF_INJECTED = 0x01;
    public const uint LLKHF_INJECTED = 0x10;
    public const uint LLKHF_UP = 0x80;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const int VK_LBUTTON = 0x01;
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOPMOST = 0x00000008;

    public delegate nint HookProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookExW(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    public static extern nint GetModuleHandleW(string? name);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    public static extern nint WindowFromPoint(POINT p);

    [DllImport("user32.dll")]
    public static extern nint GetAncestor(nint hwnd, uint flags);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    public static extern nint GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hwnd, int cmd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(nint process, uint access, out nint token);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(nint token, int infoClass, out int info, int length, out int returned);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);

    // GDI screen capture of one pixel row (CAPTUREBLT so layered windows such as the lens are included).
    [DllImport("user32.dll")] public static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] public static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] public static extern nint CreateCompatibleBitmap(nint dc, int w, int h);
    [DllImport("gdi32.dll")] public static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] public static extern bool BitBlt(nint dst, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] public static extern int GetDIBits(nint dc, nint bmp, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFOHEADER info, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }

    /// <summary>Brightness (0-255) of each pixel in one screen row.</summary>
    public static byte[] CaptureRow(int x, int y, int width)
    {
        var screen = GetDC(0);
        var mem = CreateCompatibleDC(screen);
        var bmp = CreateCompatibleBitmap(screen, width, 1);
        var old = SelectObject(mem, bmp);
        BitBlt(mem, 0, 0, width, 1, screen, x, y, 0x00CC0020 /* SRCCOPY */ | 0x40000000 /* CAPTUREBLT */);
        SelectObject(mem, old);
        var info = new BITMAPINFOHEADER { Size = 40, Width = width, Height = -1, Planes = 1, BitCount = 32 };
        var bits = new byte[width * 4];
        GetDIBits(mem, bmp, 0, 1, bits, ref info, 0);
        DeleteObject(bmp);
        DeleteDC(mem);
        ReleaseDC(0, screen);
        var row = new byte[width];
        for (var i = 0; i < width; i++)
            row[i] = (byte)((bits[i * 4] + bits[i * 4 + 1] + bits[i * 4 + 2]) / 3);
        return row;
    }
}
