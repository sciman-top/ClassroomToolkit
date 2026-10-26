using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ClassroomToolkit.Interop;
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

            // 全系统按键均过此回调：先裸读键码与注入标记做过滤，绝大多数按键
            // 免去整结构 marshal 分配（字段均为 uint，无前置 padding）。
            var key = (VirtualKey)(uint)Marshal.ReadInt32(lParam, KbdHookVkCodeOffset);
            if (!_allowedKeys.Contains(key))
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            var flags = (uint)Marshal.ReadInt32(lParam, KbdHookFlagsOffset);
            if (WpsHookKeyboardInjectionPolicy.ShouldIgnore(flags))
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            if (IsKeyboardKeySuppressed(key))
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            var consumeAuthorizedInput = IsAuthorizedInputForeground();
            if (isDown)
            {
                var direction = key is VirtualKey.Up or VirtualKey.Left or VirtualKey.PageUp ? -1 : 1;
                QueueNavigationRequest(direction, "keyboard");
            }
            if (_blockOnly || consumeAuthorizedInput)
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

            var consumeAuthorizedInput = IsAuthorizedInputForeground();
            var direction = delta < 0 ? 1 : -1;
            QueueNavigationRequest(direction, "wheel");
            if (_blockOnly || consumeAuthorizedInput)
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

    // 滚轮同方向在途合并标记（0=无在途）：惯性触摸板的高频刻度（~120/s）不再线性放大
    // 后台派发与闭包分配；上层 200ms 防抖窗口内的多刻度本就合并为一页。
    private int _inFlightWheelDirection;

    internal void QueueNavigationRequest(int direction, string source)
    {
        if (_disposed || !_interceptEnabled)
        {
            return;
        }

        if (string.Equals(source, "wheel", StringComparison.Ordinal)
            && Interlocked.CompareExchange(ref _inFlightWheelDirection, direction, 0) != 0)
        {
            // 已有同/异方向刻度在途：同方向合并跳过，异方向由在途请求先行。
            return;
        }

        var generation = Volatile.Read(ref _dispatchGeneration);
        var request = new WpsNavigationRequest(
            direction,
            source,
            CaptureForegroundWindow(),
            Stopwatch.GetTimestamp());
        var queueName = string.Equals(source, "wheel", StringComparison.Ordinal)
            ? "WpsSlideshowNavigationHook.QueueNavigationRequest.wheel"
            : "WpsSlideshowNavigationHook.QueueNavigationRequest.keyboard";
        _queueBackgroundWork(
            queueName,
            () =>
            {
                try
                {
                    if (_disposed || !_interceptEnabled || generation != Volatile.Read(ref _dispatchGeneration))
                    {
                        return;
                    }
                    InteropEventDispatchPolicy.InvokeSafely(
                        NavigationRequestCaptured,
                        request,
                        "WpsSlideshowNavigationHook.NavigationRequestCaptured");
                    InteropEventDispatchPolicy.InvokeSafely(
                        NavigationRequested,
                        direction,
                        source,
                        "WpsSlideshowNavigationHook.NavigationRequested");
                }
                finally
                {
                    if (string.Equals(source, "wheel", StringComparison.Ordinal))
                    {
                        Interlocked.Exchange(ref _inFlightWheelDirection, 0);
                    }
                }
            },
            ex => InteropHookDiagnostics.RecordCallbackException(
                component: "WpsNavHook",
                source: $"{source}_async",
                ex,
                ref _callbackExceptionCount,
                ref _lastExceptionLogTick,
                ExceptionLogIntervalMs));
    }

    private static IntPtr CaptureForegroundWindow()
    {
        return OperatingSystem.IsWindows()
            ? NativeMethods.GetForegroundWindow()
            : IntPtr.Zero;
    }
}
