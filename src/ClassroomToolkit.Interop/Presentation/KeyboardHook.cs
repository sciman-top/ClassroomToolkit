using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly HookProc _hookProc;
    private IntPtr _hookId;
    private bool _leftShiftDown;
    private bool _rightShiftDown;
    private bool _leftCtrlDown;
    private bool _rightCtrlDown;
    private bool _leftAltDown;
    private bool _rightAltDown;

    public bool IsActive => _hookId != IntPtr.Zero;

    public int LastError { get; private set; }

    public KeyboardHook()
    {
        _hookProc = HookCallback;
    }

    public KeyBinding? TargetBinding { get; set; }

    public bool SuppressWhenMatched { get; set; }

    public event Action<KeyBinding>? BindingTriggered;

    public void Start()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        if (_hookId != IntPtr.Zero)
        {
            return;
        }
        _hookId = SetHook(_hookProc);
        if (_hookId == IntPtr.Zero)
        {
            LastError = Marshal.GetLastWin32Error();
        }
        else
        {
            LastError = 0;
        }
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                var isDown = msg == WmKeyDown || msg == WmSysKeyDown;
                var isUp = msg == WmKeyUp || msg == WmSysKeyUp;
                var data = Marshal.PtrToStructure<KbdHookStruct>(lParam);
                if (isDown || isUp)
                {
                    UpdateModifiers((VirtualKey)data.VirtualKeyCode, isDown);
                }
                if (isDown)
                {
                    var key = MapKey((VirtualKey)data.VirtualKeyCode);
                    if (key.HasValue)
                    {
                        var modifiers = CurrentModifiers();
                        var binding = new KeyBinding(key.Value, modifiers);
                        if (TargetBinding != null && binding.Equals(TargetBinding))
                        {
                            BindingTriggered?.Invoke(binding);
                            if (SuppressWhenMatched)
                            {
                                return new IntPtr(1);
                            }
                        }
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        catch
        {
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }

    private void UpdateModifiers(VirtualKey key, bool isDown)
    {
        switch (key)
        {
            case VirtualKey.Shift:
                RefreshModifierState();
                break;
            case VirtualKey.LeftShift:
                _leftShiftDown = isDown;
                break;
            case VirtualKey.RightShift:
                _rightShiftDown = isDown;
                break;
            case VirtualKey.Control:
                RefreshModifierState();
                break;
            case VirtualKey.LeftControl:
                _leftCtrlDown = isDown;
                break;
            case VirtualKey.RightControl:
                _rightCtrlDown = isDown;
                break;
            case VirtualKey.Alt:
                RefreshModifierState();
                break;
            case VirtualKey.LeftAlt:
                _leftAltDown = isDown;
                break;
            case VirtualKey.RightAlt:
                _rightAltDown = isDown;
                break;
        }
    }

    private void RefreshModifierState()
    {
        _leftShiftDown = IsKeyDown(VirtualKey.LeftShift);
        _rightShiftDown = IsKeyDown(VirtualKey.RightShift);
        _leftCtrlDown = IsKeyDown(VirtualKey.LeftControl);
        _rightCtrlDown = IsKeyDown(VirtualKey.RightControl);
        _leftAltDown = IsKeyDown(VirtualKey.LeftAlt);
        _rightAltDown = IsKeyDown(VirtualKey.RightAlt);
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        return (GetKeyState((int)key) & 0x8000) != 0;
    }

    private KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (_leftShiftDown || _rightShiftDown)
        {
            modifiers |= KeyModifiers.Shift;
        }
        if (_leftCtrlDown || _rightCtrlDown)
        {
            modifiers |= KeyModifiers.Control;
        }
        if (_leftAltDown || _rightAltDown)
        {
            modifiers |= KeyModifiers.Alt;
        }
        return modifiers;
    }

    private static VirtualKey? MapKey(VirtualKey key)
    {
        if (key is VirtualKey.Shift or VirtualKey.Control or VirtualKey.Alt
            or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.LeftAlt or VirtualKey.RightAlt)
        {
            return null;
        }
        return Enum.IsDefined(typeof(VirtualKey), key) ? key : null;
    }

    private static IntPtr SetHook(HookProc proc)
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
        return SetWindowsHookEx(WhKeyboardLl, proc, moduleHandle, 0);
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

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
