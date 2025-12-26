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
    private bool _shiftDown;
    private bool _ctrlDown;
    private bool _altDown;

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

    private void UpdateModifiers(VirtualKey key, bool isDown)
    {
        switch (key)
        {
            case VirtualKey.Shift:
                _shiftDown = isDown;
                break;
            case VirtualKey.Control:
                _ctrlDown = isDown;
                break;
            case VirtualKey.Alt:
                _altDown = isDown;
                break;
        }
    }

    private KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (_shiftDown)
        {
            modifiers |= KeyModifiers.Shift;
        }
        if (_ctrlDown)
        {
            modifiers |= KeyModifiers.Control;
        }
        if (_altDown)
        {
            modifiers |= KeyModifiers.Alt;
        }
        return modifiers;
    }

    private static VirtualKey? MapKey(VirtualKey key)
    {
        if (key is VirtualKey.Shift or VirtualKey.Control or VirtualKey.Alt)
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

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
