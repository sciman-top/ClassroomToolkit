using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop;

public static class NativeMethods
{
    public const int GwlStyle = -16;
    public const int GwlExstyle = -20;
    
    public const int WsCaption = 0x00C00000;
    public const int WsMinimizeBox = 0x20000;
    
    public const uint MonitorDefaultToNearest = 2;

    public const int WsExTransparent = 0x00000020;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExNoActivate = 0x08000000;

    public const int SwpNoSize = 0x0001;
    public const int SwpNoMove = 0x0002;
    public const int SwpNoZOrder = 0x0004;
    public const int SwpNoActivate = 0x0010;
    public const int SwpFrameChanged = 0x0020;
    public const int SwpShowWindow = 0x0040;
    public const int SwpNoOwnerZOrder = 0x0200;

    public static readonly IntPtr HwndTopmost = new(-1);
    public static readonly IntPtr HwndNoTopmost = new(-2);

    // From Presentation/NativeMethods.cs
    public const int MaxClassName = 256;
    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;
    public const int WmMouseWheel = 0x020A;
    public const uint GaRoot = 2;

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    public struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    public struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    public struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    [SuppressMessage("Naming", "CA1724:Type names should not match namespaces", Justification = "Input mirrors the Win32 INPUT structure name used by existing call sites.")]
    public struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    public struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Struct is intentionally nested with Win32 declarations to preserve the NativeMethods ABI surface.")]
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop struct is used only for marshaling and is not compared by value.")]
    public struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

#pragma warning disable CA1401
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo exInfo);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out NativePoint point);
    
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);

    // Merged Methods
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hwnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hwnd, [Out] char[] className, int maxCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, [Out] char[] text, int maxCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint inputs, [MarshalAs(UnmanagedType.LPArray), In] Input[] input, int size);
#pragma warning restore CA1401
}
