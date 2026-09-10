using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private const int MaxQueuedWpsRequestAgeMs = 500;

    private void OnWpsNavigationRequestCaptured(WpsNavigationRequest request)
    {
        if (request.Direction == 0 || request.ForegroundWindow == IntPtr.Zero)
        {
            return;
        }

        var ageMs = (Stopwatch.GetTimestamp() - request.CapturedTimestampTicks)
            * 1000.0 / Stopwatch.Frequency;
        if (ageMs < 0 || ageMs > MaxQueuedWpsRequestAgeMs)
        {
            Debug.WriteLine($"[WpsNavHook] ignored stale request source={request.Source} ageMs={ageMs:0.##}");
            return;
        }

        var currentForeground = _presentationResolver.ResolveForeground();
        if (!currentForeground.IsValid || currentForeground.Handle != request.ForegroundWindow)
        {
            Debug.WriteLine($"[WpsNavHook] ignored focus-changed request source={request.Source}");
            return;
        }

        OnWpsNavHookRequested(
            request.Direction,
            request.Source,
            request.ForegroundWindow);
    }

    private void OnWpsNavHookRequested(int direction, string source)
    {
        OnWpsNavHookRequested(direction, source, capturedForegroundWindow: null);
    }

    private void OnWpsNavHookRequested(
        int direction,
        string source,
        IntPtr? capturedForegroundWindow)
    {
        void ExecuteHookRequest()
        {
            if (!_presentationOptions.AllowWps)
            {
                Debug.WriteLine($"[WpsNavHook] ignored allow=false source={source} dir={direction}");
                return;
            }
            var currentForeground = _presentationResolver.ResolveForeground();
            if (capturedForegroundWindow.HasValue
                && (!currentForeground.IsValid
                    || currentForeground.Handle != capturedForegroundWindow.Value))
            {
                Debug.WriteLine($"[WpsNavHook] ignored dispatch-focus-changed source={source} dir={direction}");
                return;
            }
            MarkWpsHookInput();
            if (IsBoardActive() || direction == 0)
            {
                Debug.WriteLine($"[WpsNavHook] ignored board={IsBoardActive()} dir={direction}");
                return;
            }
            if (source == "wheel" && ShouldSuppressPresentationWheelFromRecentInkInput())
            {
                Debug.WriteLine($"[WpsNavHook] ignored recent-ink source={source} dir={direction}");
                return;
            }
            var target = ResolveWpsTarget();
            if (!target.IsValid)
            {
                Debug.WriteLine($"[WpsNavHook] target invalid source={source} dir={direction}");
                return;
            }
            if (WpsHookNavigationInjectionGatePolicy.ShouldSuppressInjection(
                    targetIsForeground: IsTargetForeground(target),
                    foregroundInputAuthorized: IsPresentationInputFocusAuthorized(
                        capturedForegroundWindow ?? currentForeground.Handle),
                    wheelSource: source == "wheel",
                    wheelAsKeyEnabled: _presentationOptions.WheelAsKey))
            {
                // 真实输入已直达前台放映窗（hook 不吞键），再注入必然双翻页；
                // 外来应用前台时注入会把无关输入误转为翻页。
                Debug.WriteLine($"[WpsNavHook] injection-suppressed source={source} dir={direction}");
                return;
            }
            var allowBackground = IsTargetForeground(target)
                                  || IsPresentationInputFocusAuthorized(
                                      capturedForegroundWindow ?? currentForeground.Handle);
            if (!CanSendPresentationNavigation(
                    allowChannel: _presentationOptions.AllowWps,
                    target,
                    allowBackground,
                    expectedType: PresentationType.Wps))
            {
                Debug.WriteLine($"[WpsNavHook] admission-failed source={source} dir={direction}");
                return;
            }
            if (ShouldSuppressWpsNav(direction, target.Handle))
            {
                Debug.WriteLine($"[WpsNavHook] suppressed source={source} dir={direction}");
                return;
            }
            var command = direction > 0
                ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
                : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
            var options = BuildWpsOptions(source);
            var sent = TrySendPresentationCommandToTarget(target, command, options);
            if (sent)
            {
                RememberWpsNav(direction, target.Handle);
                RequestPresentationOverlayRetouchIfNeeded(true, $"wps-nav:{source}:{direction}");
                LogPresentationState($"wps-nav:{source}:{direction}");
                Debug.WriteLine($"[WpsNavHook] sent source={source} dir={direction}");
            }
            else
            {
                Debug.WriteLine($"[WpsNavHook] send failed source={source} dir={direction}");
            }
        }

        var scheduled = TryBeginInvoke(ExecuteHookRequest, System.Windows.Threading.DispatcherPriority.Normal);
        if (!scheduled)
        {
            if (Dispatcher.CheckAccess())
            {
                ExecuteHookRequest();
            }
            else
            {
                Debug.WriteLine($"[WpsNavHook] dispatch failed source={source} dir={direction}");
            }
        }
    }

    private bool TrySendWpsNavigation(ClassroomToolkit.Services.Presentation.PresentationCommand command)
    {
        if (!_presentationOptions.AllowWps)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        var target = ResolveWpsTarget();
        if (!target.IsValid)
        {
            return false;
        }
        return TrySendWpsNavigation(command, target, allowBackground: false);
    }

    private bool TrySendWpsNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        PresentationTarget target,
        bool allowBackground)
    {
        if (!CanSendPresentationNavigation(
                allowChannel: _presentationOptions.AllowWps,
                target,
                allowBackground,
                expectedType: PresentationType.Wps))
        {
            return false;
        }
        var direction = command == ClassroomToolkit.Services.Presentation.PresentationCommand.Next ? 1 : -1;
        if (ShouldSuppressWpsNav(direction, target.Handle))
        {
            return false;
        }
        var options = BuildWpsOptions("wheel");
        var sent = TrySendPresentationCommandToTarget(target, command, options);
        if (sent)
        {
            RememberWpsNav(direction, target.Handle);
        }
        return sent;
    }

    private ClassroomToolkit.Services.Presentation.PresentationControlOptions BuildWpsOptions(string? source = null)
    {
        return _presentationInputPipeline.BuildWpsOptions(_presentationOptions, source);
    }

    private void UpdateWpsNavHookState()
    {
        var generation = _wpsNavHookStateGate.NextGeneration();
        // 协程体读取 IsVisible/IsBoardActive 等 UI 状态且最终安装 LL 钩子，必须保持在 UI 线程。
        _ = _wpsNavHookStateGate.RunAsync(generation, UpdateWpsNavHookStateCoreAsync, continueOnCapturedContext: true);
    }

    private async Task UpdateWpsNavHookStateCoreAsync(Func<bool> isCurrent)
    {
        if (!isCurrent())
        {
            return;
        }
        if (_wpsNavHookClient == null || !_wpsNavHookClient.Available)
        {
            _wpsNavHookActive = false;
            if (_presentationOptions.AllowWps)
            {
                var hookTarget = ResolveWpsTarget();
                MarkWpsHookUnavailable(hookTarget.IsValid);
            }
            LogPresentationState("wps-hook-unavailable");
            return;
        }
        if (!isCurrent())
        {
            return;
        }
        _presentationInputPipeline.ResetWpsHookFallback();
        var shouldEnable = WpsHookEnableGatePolicy.ShouldAttemptResolveTarget(
            _presentationOptions.AllowWps,
            IsBoardActive(),
            IsVisible,
            _photoModeActive);
        var target = PresentationTarget.Empty;
        if (shouldEnable)
        {
            target = ResolveWpsTarget();
            shouldEnable = WpsHookEnableGatePolicy.ShouldEnableWithTarget(
                shouldEnable,
                target.IsValid,
                IsPresentationSlideshow(target, PresentationType.Wps));
        }
        var sendMode = InputStrategy.Message;
        var wheelForward = false;
        if (shouldEnable)
        {
            sendMode = ResolveWpsSendMode(target);
            wheelForward = _presentationOptions.WheelAsKey;
        }

        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable,
            _mode,
            targetIsSlideshow: shouldEnable,
            targetForeground: shouldEnable && IsTargetForeground(target),
            isRawSendMode: sendMode == InputStrategy.Raw,
            wheelForward);
        if (shouldEnable)
        {
            var runtimeState = _wpsHookOrchestrator.ApplyEnabled(
                _wpsNavHookClient,
                decision,
                _wpsNavHookActive,
                ResolveAuthorizedPresentationInputWindows());
            ApplyWpsHookRuntimeState(runtimeState);
            if (!runtimeState.ConfigurationApplied)
            {
                MarkWpsHookUnavailable(target.IsValid);
                LogPresentationState("wps-hook-configuration-failed");
                return;
            }
            var startResult = _wpsNavHookActive;
            if (!_wpsNavHookActive)
            {
                // 不用 ConfigureAwait(false)：StartAsync 内部重试依赖调用方上下文，
                // LL 钩子必须回到 UI 线程安装；后续 Stop/状态回写也读取 UI 状态。
                startResult = await _wpsHookOrchestrator.TryStartSafeAsync(_wpsNavHookClient);
            }
            if (!isCurrent())
            {
                // StartAsync 的内部重试可能在 generation 失效后才完成；旧操作不能
                // 把已安装的 native hook 留给下一模式。补偿停止并保留残留状态。
                var staleCleanup = _wpsHookOrchestrator.ApplyDisabled(_wpsNavHookClient);
                ApplyWpsHookRuntimeState(staleCleanup);
                return;
            }
            _wpsNavHookActive = startResult;
            if (!_wpsNavHookActive)
            {
                StopWpsNavHook();
                MarkWpsHookUnavailable(target.IsValid);
            }
            else
            {
                _presentationInputPipeline.ResetWpsHookFallback();
                WpsHookUnavailableNotificationPolicy.Reset(ref _wpsHookUnavailableNotifiedState);
            }
            LogPresentationState($"wps-hook-enabled:{sendMode}");
            return;
        }
        if (!isCurrent())
        {
            return;
        }
        StopWpsNavHook();
        LogPresentationState("wps-hook-disabled");
    }

    private void StopWpsNavHook()
    {
        var runtimeState = _wpsHookOrchestrator.ApplyDisabled(_wpsNavHookClient);
        ApplyWpsHookRuntimeState(runtimeState);
    }

    private void ApplyWpsHookRuntimeState(WpsHookRuntimeState state)
    {
        _wpsHookBlockOnly = state.BlockOnly;
        _wpsNavHookActive = state.IsActive;
        _wpsHookInterceptKeyboard = state.InterceptKeyboard;
        _wpsHookInterceptWheel = state.InterceptWheel;
    }

    private PresentationTarget ResolveWpsTarget()
    {
        return _presentationTargetSessionBinding.Resolve(
            PresentationType.Wps,
            resolveCandidate: () => _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: true,
                allowOffice: false,
                _currentProcessId),
            isAdmitted: target =>
                IsAdmittedWpsTarget(target));
    }

    private bool IsAdmittedWpsTarget(PresentationTarget target)
    {
        return _presentationTargetAdmission(target, PresentationType.Wps);
    }

    private bool IsPresentationInputFocusAuthorized(IntPtr foregroundWindow)
    {
        return PresentationInputFocusPolicy.IsAuthorizedForeground(
            foregroundWindow,
            _hwnd,
            ResolveToolbarWindowHandle());
    }

    private static IntPtr ResolveToolbarWindowHandle()
    {
        var windows = System.Windows.Application.Current?.Windows;
        if (windows == null)
        {
            return IntPtr.Zero;
        }

        foreach (Window window in windows)
        {
            if (window is not PaintToolbarWindow toolbar || !toolbar.IsVisible)
            {
                continue;
            }

            if (PresentationSource.FromVisual(toolbar) is HwndSource source
                && source.Handle != IntPtr.Zero)
            {
                return source.Handle;
            }
        }

        return IntPtr.Zero;
    }

    private List<IntPtr> ResolveAuthorizedPresentationInputWindows()
    {
        var windows = new List<IntPtr>(capacity: 2);
        if (_hwnd != IntPtr.Zero && IsVisible)
        {
            windows.Add(_hwnd);
        }

        var toolbarHandle = ResolveToolbarWindowHandle();
        if (toolbarHandle != IntPtr.Zero && !windows.Contains(toolbarHandle))
        {
            windows.Add(toolbarHandle);
        }

        return windows;
    }

    public void RefreshPresentationInputOwnership()
    {
        if (_wpsNavHookClient == null)
        {
            return;
        }

        SafeActionExecutionExecutor.TryExecute(
            () => _wpsNavHookClient.SetAuthorizedInputWindows(ResolveAuthorizedPresentationInputWindows()),
            ex => Debug.WriteLine(
                $"[WpsNavHook] authorized-window refresh failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private InputStrategy ResolveWpsSendMode(
        PresentationTarget target)
    {
        return _presentationInputPipeline.ResolveWpsSendMode(target.IsValid, target.Handle);
    }

    private void MarkWpsHookUnavailable(bool notify)
    {
        _presentationInputPipeline.MarkWpsHookUnavailable();
        if (notify)
        {
            NotifyWpsHookUnavailable();
        }
    }

    private void NotifyWpsHookUnavailable()
    {
        if (!WpsHookUnavailableNotificationPolicy.ShouldNotify(ref _wpsHookUnavailableNotifiedState))
        {
            return;
        }
        void ShowUnavailableMessage()
        {
            SafeActionExecutionExecutor.TryExecute(
                () => MessageBoxWpsHookUnavailableNotifier.Notify(this),
                ex => Debug.WriteLine(
                    $"[WpsNavHook] unavailable message failed: {ex.GetType().Name} - {ex.Message}"));
        }

        var scheduled = TryBeginInvoke(ShowUnavailableMessage, System.Windows.Threading.DispatcherPriority.Background);
        if (!scheduled)
        {
            if (Dispatcher.CheckAccess())
            {
                ShowUnavailableMessage();
            }
            else
            {
                Debug.WriteLine("[WpsNavHook] unavailable message dispatch failed");
            }
        }
    }

    private static bool IsTargetForeground(PresentationTarget target)
    {
        if (!target.IsValid)
        {
            return false;
        }
        return PresentationForegroundSuppressionInteropAdapter.IsForeground(target.Handle);
    }

    private bool ShouldSuppressWpsNav(int direction, IntPtr target)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        return WpsNavigationDebouncePolicy.ShouldSuppress(
            direction,
            target,
            nowUtc,
            new WpsNavigationDebounceState(_lastWpsNavEvent),
            WpsNavDebounceMs);
    }

    private void RememberWpsNav(int direction, IntPtr target)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var state = WpsNavigationDebouncePolicy.Remember(
            direction,
            target,
            nowUtc);
        WpsNavigationDebounceStateUpdater.Apply(
            ref _lastWpsNavEvent,
            state);
    }

    private void MarkWpsHookInput()
    {
        _lastWpsHookInput = GetCurrentUtcTimestamp();
    }

    private bool WpsHookRecentlyFired()
    {
        return WpsHookInputDebouncePolicy.IsRecent(
            _lastWpsHookInput,
            GetCurrentUtcTimestamp(),
            WpsNavDebounceMs);
    }
}
