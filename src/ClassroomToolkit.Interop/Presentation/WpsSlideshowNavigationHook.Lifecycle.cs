using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class WpsSlideshowNavigationHook
{
    public async Task<bool> StartAsync()
    {
        if (_disposed || !Available)
        {
            return false;
        }
        if (_keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero)
        {
            return true;
        }

        var moduleHandle = GetModuleHandle(null);
        for (var attempt = 0; attempt < MaxHookRetries; attempt++)
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
                var delayMs = 50 * (1 << attempt); // Exponential backoff.
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        NavigationRequested = null;
        GC.SuppressFinalize(this);
    }
}
