using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using ClassroomToolkit.Interop.Utilities;

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
    
    private const int HookCallbackTimeoutMs = 50;
    private const int MaxHookRetries = 3;
    private const int ExceptionLogIntervalMs = 30000;

    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _interceptEnabled;
    private bool _blockOnly;
    private bool _interceptKeyboard = true;
    private bool _interceptWheel = true;
    private bool _emitWheelOnBlock = true;
    private int _callbackExceptionCount;
    private long _lastExceptionLogTick;
    private int _dispatchGeneration;
    private volatile bool _disposed;
    public int LastError { get; private set; }
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
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    public event Action<int, string>? NavigationRequested;

    public bool Available => OperatingSystem.IsWindows();

    public void SetInterceptEnabled(bool enabled) => _interceptEnabled = enabled;

    public void SetBlockOnly(bool enabled) => _blockOnly = enabled;

    public void SetInterceptKeyboard(bool enabled) => _interceptKeyboard = enabled;

    public void SetInterceptWheel(bool enabled) => _interceptWheel = enabled;

    public void SetEmitWheelOnBlock(bool enabled) => _emitWheelOnBlock = enabled;

    public async Task<bool> StartAsync()
    {
        if (_disposed)
        {
            return false;
        }
        if (!Available)
        {
            return false;
        }
        if (_keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero)
        {
            return true;
        }
        var moduleHandle = GetModuleHandle(null);
        for (int attempt = 0; attempt < MaxHookRetries; attempt++)
        {
            if (_disposed)
            {
                Stop();
                return false;
            }
            if (_keyboardHook == IntPtr.Zero)
            {
                _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
            }
            if (_mouseHook == IntPtr.Zero)
            {
                _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);
            }
            if (_keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero)
            {
                LastError = 0;
                return true;
            }
            if (attempt < MaxHookRetries - 1)
            {
                var delayMs = 50 * (1 << attempt); // Exponential backoff
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }
        LastError = Marshal.GetLastWin32Error();
        Stop();
        return false;
    }

    public void Stop()
    {
        _interceptEnabled = false;
        _blockOnly = false;
        _interceptKeyboard = true;
        _interceptWheel = true;
        _emitWheelOnBlock = true;
        Interlocked.Increment(ref _dispatchGeneration);
        var unhookFailed = false;
        var lastUnhookError = 0;
        if (_keyboardHook != IntPtr.Zero)
        {
            if (!UnhookWindowsHookEx(_keyboardHook))
            {
                unhookFailed = true;
                lastUnhookError = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[WpsNavHook] Keyboard unhook failed with error={lastUnhookError}");
            }
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            if (!UnhookWindowsHookEx(_mouseHook))
            {
                unhookFailed = true;
                lastUnhookError = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[WpsNavHook] Mouse unhook failed with error={lastUnhookError}");
            }
            _mouseHook = IntPtr.Zero;
        }
        LastError = unhookFailed ? lastUnhookError : 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        NavigationRequested = null;
        GC.SuppressFinalize(this);
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var startTime = Stopwatch.GetTimestamp();
        try
        {
            if (nCode != HcAction || !_interceptEnabled || !_interceptKeyboard)
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }
            if (lParam == IntPtr.Zero)
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
            if (WpsHookKeyboardInjectionPolicy.ShouldIgnore(data.Flags))
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }
            var key = (VirtualKey)data.VirtualKeyCode;
            if (!_allowedKeys.Contains(key))
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }
            if (isDown)
            {
                var direction = key is VirtualKey.Up or VirtualKey.Left or VirtualKey.PageUp ? -1 : 1;
                QueueNavigationRequest(direction, "keyboard");
            }
            if (_blockOnly)
            {
                return new IntPtr(1);
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
        catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
        {
            RecordCallbackException("keyboard", ex);
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
        finally
        {
             var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
             if (elapsedMs > HookCallbackTimeoutMs)
             {
                 Debug.WriteLine($"[WpsNavHook] Keyboard callback took {elapsedMs:F1}ms");
             }
        }
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var startTime = Stopwatch.GetTimestamp();
        try
        {
            if (nCode != HcAction || !_interceptEnabled || !_interceptWheel)
            {
                return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }
            if (lParam == IntPtr.Zero)
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
            QueueNavigationRequest(direction, "wheel");
            if (_blockOnly)
            {
                return new IntPtr(1);
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
        {
            RecordCallbackException("mouse", ex);
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        finally
        {
             var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
             if (elapsedMs > HookCallbackTimeoutMs)
             {
                 Debug.WriteLine($"[WpsNavHook] Mouse callback took {elapsedMs:F1}ms");
             }
        }
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
        Debug.WriteLine($"[WpsNavHook][{source}] callback exception count={count}, type={ex.GetType().Name}, message={ex.Message}");
    }

    private void QueueNavigationRequest(int direction, string source)
    {
        if (_disposed || !_interceptEnabled)
        {
            return;
        }
        var generation = Volatile.Read(ref _dispatchGeneration);
        InteropBackgroundDispatchExecutor.Queue(
            $"WpsSlideshowNavigationHook.QueueNavigationRequest.{source}",
            () =>
            {
                if (_disposed || !_interceptEnabled || generation != Volatile.Read(ref _dispatchGeneration))
                {
                    return;
                }
                InteropEventDispatchPolicy.InvokeSafely(
                    NavigationRequested,
                    direction,
                    source,
                    "WpsSlideshowNavigationHook.NavigationRequested");
            },
            ex => RecordCallbackException($"{source}_async", ex));
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
