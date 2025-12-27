using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public sealed class WpsSlideshowNavigationHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int HcAction = 0;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseWheel = 0x020A;

    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _interceptEnabled;
    private bool _blockOnly;
    private bool _interceptKeyboard = true;
    private bool _interceptWheel = true;
    private bool _emitWheelOnBlock = true;
    private readonly HashSet<VirtualKey> _allowedKeys = new()
    {
        VirtualKey.Up,
        VirtualKey.Down,
        VirtualKey.Left,
        VirtualKey.Right,
        VirtualKey.PageUp,
        VirtualKey.PageDown,
        VirtualKey.Space,
        VirtualKey.Enter
    };

    public WpsSlideshowNavigationHook()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
    }

    public event Action<int, string>? NavigationRequested;

    public bool Available => OperatingSystem.IsWindows();

    public void SetInterceptEnabled(bool enabled) => _interceptEnabled = enabled;

    public void SetBlockOnly(bool enabled) => _blockOnly = enabled;

    public void SetInterceptKeyboard(bool enabled) => _interceptKeyboard = enabled;

    public void SetInterceptWheel(bool enabled) => _interceptWheel = enabled;

    public void SetEmitWheelOnBlock(bool enabled) => _emitWheelOnBlock = enabled;

    public bool Start()
    {
        if (!Available)
        {
            return false;
        }
        if (_keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero)
        {
            return true;
        }
        var moduleHandle = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Stop();
            return false;
        }
        return true;
    }

    public void Stop()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _interceptEnabled = false;
        _blockOnly = false;
        _interceptKeyboard = true;
        _interceptWheel = true;
        _emitWheelOnBlock = true;
    }

    public void Dispose() => Stop();

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HcAction || !_interceptEnabled || !_interceptKeyboard)
        {
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
        var msg = wParam.ToInt32();
        var isDown = msg == WmKeyDown || msg == WmSysKeyDown;
        var isUp = msg == WmKeyUp || msg == WmSysKeyUp;
        if (!isDown && !isUp)
        {
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
        var data = Marshal.PtrToStructure<KbdHookStruct>(lParam);
        var key = (VirtualKey)data.VirtualKeyCode;
        if (!_allowedKeys.Contains(key))
        {
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
        if (isDown)
        {
            var direction = key is VirtualKey.Up or VirtualKey.Left or VirtualKey.PageUp ? -1 : 1;
            NavigationRequested?.Invoke(direction, "keyboard");
        }
        if (_blockOnly)
        {
            return new IntPtr(1);
        }
        return new IntPtr(1);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HcAction || !_interceptEnabled || !_interceptWheel)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        if (wParam.ToInt32() != WmMouseWheel)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
        var delta = (short)((data.MouseData >> 16) & 0xFFFF);
        if (delta == 0)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        if (_blockOnly && !_emitWheelOnBlock)
        {
            return new IntPtr(1);
        }
        var direction = delta < 0 ? 1 : -1;
        NavigationRequested?.Invoke(direction, "wheel");
        return new IntPtr(1);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
