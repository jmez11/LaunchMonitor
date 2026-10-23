using System.Runtime.InteropServices;
using System.Text;
using LaunchMonitor.Core;

namespace LaunchMonitor;

internal static class Native
{
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventMoveSizeStart = 0x000A;
    internal const int WhMouseLl = 14;
    internal const int WhKeyboardLl = 13;
    internal const int GwlStyle = -16;
    internal const int GwlExStyle = -20;
    internal const long WsChild = 0x40000000;
    internal const long WsExToolWindow = 0x80;
    internal const long WsExNoActivate = 0x08000000;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpNoOwnerZOrder = 0x0200;
    internal const uint SwpAsyncWindowPos = 0x4000;

    internal delegate nint HookProc(int code, nint message, nint data);
    internal delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId,
        int childId, uint threadId, uint time);
    internal delegate bool EnumWindowsProc(nint window, nint data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { internal int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left, Top, Right, Bottom;
        internal readonly DesktopRect ToDesktopRect() => new(Left, Top, Right - Left, Bottom - Top);
        internal static Rect From(DesktopRect rect) => new()
        { Left = rect.X, Top = rect.Y, Right = rect.Right, Bottom = rect.Bottom };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal int Size;
        internal Rect Monitor, Work;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Placement
    {
        internal int Length, Flags, ShowCommand;
        internal Point MinPosition, MaxPosition;
        internal Rect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseData
    {
        internal Point Position;
        internal uint Mouse, Flags, Time;
        internal nuint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardData
    {
        internal uint Key, Scan, Flags, Time;
        internal nuint Extra;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookExW(int type, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWinEventHook(uint minimum, uint maximum, nint module,
        WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint data);
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsZoomed(nint window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    internal static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassNameW(nint window, StringBuilder name, int count);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPlacement(nint window, in Placement placement);
}
