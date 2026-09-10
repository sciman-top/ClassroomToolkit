using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class WpsSlideshowNavigationHook
{
    public async Task<bool> StartAsync()
    {
        int startGeneration;
        lock (_lifecycleSync)
        {
            if (_disposed || !Available)
            {
                return false;
            }
            if (IsActiveUnsafe()
                && _keyboardHook != IntPtr.Zero
                && _mouseHook != IntPtr.Zero)
            {
                return true;
            }

            startGeneration = _lifecycleGeneration;
        }

        var moduleHandle = GetModuleHandle(null);
        var lastInstallError = 0;
        for (var attempt = 0; attempt < MaxHookRetries; attempt++)
        {
            if (!IsStartGenerationCurrent(startGeneration))
            {
                return false;
            }

            var installed = false;
            lock (_lifecycleSync)
            {
                if (_disposed || _lifecycleGeneration != startGeneration)
                {
                    return false;
                }

                if (_keyboardHook == IntPtr.Zero)
                {
                    _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
                    if (_keyboardHook == IntPtr.Zero)
                    {
                        // GetLastWin32Error 是 per-thread 的，必须在安装失败的同步点立即取值，
                        // await 之后线程可能已切换，读到的是错误线程上的陈旧值。
                        lastInstallError = Marshal.GetLastWin32Error();
                    }
                }
                if (_mouseHook == IntPtr.Zero)
                {
                    _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);
                    if (_mouseHook == IntPtr.Zero)
                    {
                        lastInstallError = Marshal.GetLastWin32Error();
                    }
                }
                installed = _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;
                if (installed)
                {
                    LastError = 0;
                }
            }

            if (installed)
            {
                return IsStartGenerationCurrent(startGeneration);
            }
            if (attempt < MaxHookRetries - 1)
            {
                var delayMs = 50 * (1 << attempt); // Exponential backoff.
                // 不用 ConfigureAwait(false)：LL 钩子要求安装线程持续泵消息，
                // 回到调用方上下文（UI 线程）安装才能收到回调。
                await Task.Delay(delayMs);
            }
        }

        LastError = lastInstallError;
        Debug.WriteLine($"[WpsNavHook] Start failed with error={lastInstallError}");
        Stop();
        if (!IsActive)
        {
            LastError = lastInstallError;
        }
        return false;
    }

    private bool IsStartGenerationCurrent(int generation)
    {
        lock (_lifecycleSync)
        {
            return !_disposed && _lifecycleGeneration == generation;
        }
    }

    public void Stop()
    {
        lock (_lifecycleSync)
        {
            _lifecycleGeneration++;
            _interceptEnabled = false;
            _blockOnly = false;
            _interceptKeyboard = true;
            _interceptWheel = true;
            _emitWheelOnBlock = true;
            _consumeAuthorizedInput = false;
            SetAuthorizedInputWindows(null);
            SetSuppressedKeyboardKeys(null);
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
                    // unhook 失败意味着系统级钩子仍在位：保留句柄，让残留状态可见且可重试。
                }
                else
                {
                    _keyboardHook = IntPtr.Zero;
                }
            }
            if (_mouseHook != IntPtr.Zero)
            {
                if (!UnhookWindowsHookEx(_mouseHook))
                {
                    unhookFailed = true;
                    lastUnhookError = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"[WpsNavHook] Mouse unhook failed with error={lastUnhookError}");
                }
                else
                {
                    _mouseHook = IntPtr.Zero;
                }
            }

            LastError = unhookFailed ? lastUnhookError : 0;
        }
    }

    public void Dispose()
    {
        if (_disposed && !IsActive)
        {
            return;
        }

        _disposed = true;
        for (var attempt = 1; attempt <= MaxHookRetries; attempt++)
        {
            Stop();
            if (!IsActive)
            {
                NavigationRequested = null;
                NavigationRequestCaptured = null;
                GC.SuppressFinalize(this);
                return;
            }

            // Keep the retry bounded and non-blocking. A residual hook remains
            // observable through IsActive/LastError for the owning lifecycle to retry.
        }

        NavigationRequested = null;
        NavigationRequestCaptured = null;
        Debug.WriteLine($"[WpsNavHook] Dispose deferred; residual hook remains active error={LastError}");
    }
}
