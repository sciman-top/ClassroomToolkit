using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ClassroomToolkit.Interop.Utilities;

namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class WpsSlideshowNavigationHook
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseWheel = 0x020A;

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
            InteropHookDiagnostics.RecordCallbackException(
                component: "WpsNavHook",
                source: "keyboard",
                ex,
                ref _callbackExceptionCount,
                ref _lastExceptionLogTick,
                ExceptionLogIntervalMs);
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
        finally
        {
            InteropHookDiagnostics.LogSlowCallback("WpsNavHook.Keyboard", startTime, HookCallbackTimeoutMs);
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
            InteropHookDiagnostics.RecordCallbackException(
                component: "WpsNavHook",
                source: "mouse",
                ex,
                ref _callbackExceptionCount,
                ref _lastExceptionLogTick,
                ExceptionLogIntervalMs);
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        finally
        {
            InteropHookDiagnostics.LogSlowCallback("WpsNavHook.Mouse", startTime, HookCallbackTimeoutMs);
        }
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
            ex => InteropHookDiagnostics.RecordCallbackException(
                component: "WpsNavHook",
                source: $"{source}_async",
                ex,
                ref _callbackExceptionCount,
                ref _lastExceptionLogTick,
                ExceptionLogIntervalMs));
    }
}
