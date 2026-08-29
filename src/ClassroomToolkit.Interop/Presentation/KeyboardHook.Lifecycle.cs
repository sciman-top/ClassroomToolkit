using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class KeyboardHook
{
    public void Start()
    {
        StartCoreSync();
    }

    public Task StartAsync()
    {
        return StartCoreAsync();
    }

    private async Task StartCoreAsync()
    {
        if (_disposed || !OperatingSystem.IsWindows() || _hookId != IntPtr.Zero)
        {
            return;
        }

        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (_disposed)
            {
                Stop();
                return;
            }

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
                var delayMs = 50 * (1 << attempt); // Exponential backoff: 50, 100, 200 ms.
                // 不用 ConfigureAwait(false)：WH_KEYBOARD_LL 要求安装线程持续泵消息，
                // 回到调用方上下文（UI 线程）安装才能收到回调；线程池线程会静默失效。
                await Task.Delay(delayMs);
            }
        }

        Debug.WriteLine($"[KeyboardHook] Start failed with error={LastError}");
    }

    private void StartCoreSync()
    {
        if (_disposed || !OperatingSystem.IsWindows() || _hookId != IntPtr.Zero)
        {
            return;
        }

        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (_disposed)
            {
                Stop();
                return;
            }

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
                var delayMs = 50 * (1 << attempt); // Exponential backoff: 50, 100, 200 ms.
                _ = SpinWait.SpinUntil(static () => false, delayMs);
            }
        }

        Debug.WriteLine($"[KeyboardHook] Start failed with error={LastError}");
    }

    public void Stop()
    {
        _acceptEvents = false;
        BindingTriggered = null;
        TargetBinding = null;
        if (_hookId == IntPtr.Zero)
        {
            LastError = 0;
            return;
        }

        if (!UnhookWindowsHookEx(_hookId))
        {
            LastError = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[KeyboardHook] Unhook failed with error={LastError}");
            // unhook 失败意味着系统级钩子仍在位：保留句柄，让残留状态可见且可重试；
            // 置零会让钩子残留到进程退出且无人再尝试摘除。
            return;
        }

        LastError = 0;
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }

    private static IntPtr SetHook(HookProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
        return SetWindowsHookEx(WhKeyboardLl, proc, moduleHandle, 0);
    }
}
