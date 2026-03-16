using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Interop.Utilities;

public sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    
    // Performance monitoring
    private const int HookCallbackTimeoutMs = 50;
    private const int ExceptionLogIntervalMs = 30000;

    private readonly HookProc _hookProc;
    private IntPtr _hookId;
    private bool _leftShiftDown;
    private bool _rightShiftDown;
    private bool _leftCtrlDown;
    private bool _rightCtrlDown;
    private bool _leftAltDown;
    private bool _rightAltDown;
    private VirtualKey? _pendingSuppressedKey;
    private int _callbackExceptionCount;
    private long _lastExceptionLogTick;
    private volatile bool _acceptEvents;

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
        StartCore(async: false).GetAwaiter().GetResult();
    }

    public Task StartAsync()
    {
        return StartCore(async: true);
    }

    private async Task StartCore(bool async)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            _hookId = SetHook(_hookProc);
            if (_hookId != IntPtr.Zero)
            {
                LastError = 0;
                _acceptEvents = true;
                RefreshModifierState();
                return;
            }
            LastError = Marshal.GetLastWin32Error();
            if (attempt < maxRetries - 1)
            {
                var delayMs = 50 * (1 << attempt); // Exponential backoff: 50, 100, 200ms
                if (async)
                    await Task.Delay(delayMs);
                else
                    Thread.Sleep(delayMs);
            }
        }
    }

    public void Stop()
    {
        _acceptEvents = false;
        if (_hookId == IntPtr.Zero)
        {
            return;
        }
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _pendingSuppressedKey = null;
    }

    ~KeyboardHook()
    {
        var hook = _hookId;
        if (hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hook);
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var startTime = Stopwatch.GetTimestamp();
        try
        {
            if (!_acceptEvents || nCode < 0 || lParam == IntPtr.Zero)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            var msg = wParam.ToInt32();
            var isDown = msg == WmKeyDown || msg == WmSysKeyDown;
            var isUp = msg == WmKeyUp || msg == WmSysKeyUp;
            var data = Marshal.PtrToStructure<KbdHookStruct>(lParam);
            var virtualKey = (VirtualKey)data.VirtualKeyCode;

            if (isDown || isUp)
            {
                UpdateModifiers(virtualKey, isDown);
            }

            var bindingMatched = false;
            if (isDown)
            {
                var key = MapKey(virtualKey);
                if (key.HasValue)
                {
                    var modifiers = CurrentModifiers();
                    var binding = new KeyBinding(key.Value, modifiers);
                    if (TargetBinding != null && binding.Equals(TargetBinding))
                    {
                        if (!_acceptEvents)
                        {
                            return CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        bindingMatched = true;
                        InteropEventDispatchPolicy.InvokeSafely(
                            BindingTriggered,
                            binding,
                            "KeyboardHook.BindingTriggered");
                    }
                }
            }

            var suppressionDecision = KeyboardHookSuppressionPolicy.Resolve(
                suppressWhenMatched: SuppressWhenMatched,
                bindingMatched: bindingMatched,
                isDown: isDown,
                isUp: isUp,
                key: virtualKey,
                pendingSuppressedKey: _pendingSuppressedKey);
            _pendingSuppressedKey = suppressionDecision.PendingSuppressedKey;
            if (suppressionDecision.ShouldSuppress)
            {
                return new IntPtr(1);
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
        {
            RecordCallbackException("keyboard", ex);
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        finally
        {
             var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
             if (elapsedMs > HookCallbackTimeoutMs)
             {
                 Debug.WriteLine($"[KeyboardHook] Callback took {elapsedMs:F1}ms");
             }
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
        // Consistency check: Verified against actual system state
        // This prevents "stuck" modifiers if a Up event was missed
        bool inconsistent = 
            (_leftShiftDown != IsKeyDown(VirtualKey.LeftShift)) ||
            (_rightShiftDown != IsKeyDown(VirtualKey.RightShift)) ||
            (_leftCtrlDown != IsKeyDown(VirtualKey.LeftControl)) ||
            (_rightCtrlDown != IsKeyDown(VirtualKey.RightControl)) ||
            (_leftAltDown != IsKeyDown(VirtualKey.LeftAlt)) ||
            (_rightAltDown != IsKeyDown(VirtualKey.RightAlt));

        if (inconsistent)
        {
             Debug.WriteLine("[KeyboardHook] Modifier state inconsistency detected. Forcing refresh.");
             RefreshModifierState();
        }

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

    private void RecordCallbackException(string source, Exception ex)
    {
        var count = Interlocked.Increment(ref _callbackExceptionCount);
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastExceptionLogTick);
        if (now - last < ExceptionLogIntervalMs)
        {
            return;
        }
        Interlocked.Exchange(ref _lastExceptionLogTick, now);
        Debug.WriteLine($"[KeyboardHook][{source}] callback exception count={count}, type={ex.GetType().Name}, message={ex.Message}");
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
