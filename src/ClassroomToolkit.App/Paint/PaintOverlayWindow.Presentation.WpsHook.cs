using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnWpsNavHookRequested(int direction, string source)
    {
        void ExecuteHookRequest()
        {
            if (!_presentationOptions.AllowWps)
            {
                Debug.WriteLine($"[WpsNavHook] ignored allow=false source={source} dir={direction}");
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
            var passthrough = IsWpsRawInputPassthrough(target);
            var interceptSource = source == "wheel" ? _wpsHookInterceptWheel : _wpsHookInterceptKeyboard;
            if (passthrough && !interceptSource)
            {
                Debug.WriteLine($"[WpsNavHook] passthrough source={source} dir={direction}");
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

        var scheduled = TryBeginInvoke(ExecuteHookRequest, System.Windows.Threading.DispatcherPriority.Background);
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
                allowBackground))
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
        _ = _wpsNavHookStateGate.RunAsync(generation, UpdateWpsNavHookStateCoreAsync);
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
                IsPresentationSlideshow(target));
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
                _wpsNavHookActive);
            ApplyWpsHookRuntimeState(runtimeState);
            if (!_wpsNavHookActive)
            {
                _wpsNavHookActive = await _wpsHookOrchestrator.TryStartSafeAsync(_wpsNavHookClient).ConfigureAwait(false);
            }
            if (!isCurrent())
            {
                return;
            }
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
        return _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            allowWps: true,
            allowOffice: false,
            _currentProcessId);
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
                () => _wpsHookUnavailableNotifier.Notify(this),
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

    private bool IsWpsRawInputPassthrough(PresentationTarget target)
    {
        if (ResolveWpsSendMode(target) != InputStrategy.Raw)
        {
            return false;
        }
        return IsTargetForeground(target);
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
            new WpsNavigationDebounceState(_lastWpsNavEvent, _wpsNavBlockUntil),
            WpsNavDebounceMs);
    }

    private void RememberWpsNav(int direction, IntPtr target)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var state = WpsNavigationDebouncePolicy.Remember(
            direction,
            target,
            nowUtc,
            WpsNavDebounceMs);
        WpsNavigationDebounceStateUpdater.Apply(
            ref _lastWpsNavEvent,
            ref _wpsNavBlockUntil,
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
