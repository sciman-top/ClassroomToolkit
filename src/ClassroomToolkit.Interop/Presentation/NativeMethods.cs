using System.Runtime.InteropServices;
using System.Text;

namespace ClassroomToolkit.Interop.Presentation;

internal static class NativeMethods
{
    public const int MaxClassName = 256;

    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;
    public const int WmMouseWheel = 0x020A;

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern uint SendInput(uint inputs, Input[] input, int size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }
}
