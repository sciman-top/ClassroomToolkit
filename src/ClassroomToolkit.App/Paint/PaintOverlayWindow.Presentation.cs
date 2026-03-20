using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void ClearCurrentPresentationType()
    {
        _currentPresentationType = PresentationType.None;
    }

    public bool RestorePresentationFocusIfNeeded(bool requireFullscreen = false)
    {
        var sessionState = _sessionCoordinator.CurrentState;
        var presentationAllowed = PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
            _presentationOptions.AllowOffice,
            _presentationOptions.AllowWps);
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        var targetIsValid = target.IsValid;
        var targetIsSlideshow = targetIsValid && _presentationClassifier.IsSlideshowWindow(target.Info);
        var targetIsFullscreen = targetIsValid && IsFullscreenPresentationWindow(target);
        var force = ShouldForcePresentationForeground(target);
        var foregroundOwned = IsForegroundOwnedByCurrentProcess();
        if (!PresentationFocusRestorePolicy.CanRestore(
                sessionState,
                _photoModeActive,
                IsBoardActive(),
                IsVisible,
                presentationAllowed,
                targetIsValid,
                targetIsSlideshow,
                targetIsFullscreen,
                requireFullscreen,
                force,
                foregroundOwned))
        {
            return false;
        }

        return PresentationWindowFocus.EnsureForeground(target.Handle);
    }

    public void ForwardKeyboardToPresentation(Key key)
    {
        if (!PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
                _presentationOptions.AllowOffice,
                _presentationOptions.AllowWps))
        {
            return;
        }
        if (!PresentationKeyCommandPolicy.TryMap(key, out var command))
        {
            return;
        }
        if (TrySendPresentationCommand(command))
        {
        }
    }

    private void UpdatePresentationFocusMonitor()
    {
        var shouldMonitor = PresentationFocusMonitorActivationPolicy.ShouldMonitor(
            overlayVisible: IsVisible,
            allowOffice: _presentationOptions.AllowOffice,
            allowWps: _presentationOptions.AllowWps,
            photoFullscreenActive: IsPhotoFullscreenActive);
        if (shouldMonitor)
        {
            if (!_presentationFocusMonitor.IsEnabled)
            {
                _presentationFocusMonitor.Start();
            }
            return;
        }
        if (_presentationFocusMonitor.IsEnabled)
        {
            _presentationFocusMonitor.Stop();
        }
    }

    private void DetectForegroundPresentation()
    {
        if (!PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
                _presentationOptions.AllowOffice,
                _presentationOptions.AllowWps))
        {
            _foregroundPresentationActive = false;
            return;
        }
        var target = _presentationResolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            _foregroundPresentationActive = false;
            return;
        }
        var type = _presentationClassifier.Classify(target.Info);
        if (type == PresentationType.None)
        {
            _foregroundPresentationActive = false;
            return;
        }
        if (!IsFullscreenPresentationWindow(target))
        {
            _foregroundPresentationActive = false;
            return;
        }
        if (_foregroundPresentationActive
            && _foregroundPresentationHandle == target.Handle
            && _foregroundPresentationType == type)
        {
            return;
        }
        _foregroundPresentationActive = true;
        _foregroundPresentationHandle = target.Handle;
        _foregroundPresentationType = type;
        SafeActionExecutionExecutor.TryExecute(
            () => PresentationForegroundDetected?.Invoke(MapPresentationForegroundSource(type)),
            ex => Debug.WriteLine($"[PresentationForegroundDetected] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private void DetectForegroundPhoto()
    {
        if (!IsPhotoFullscreenActive)
        {
            _foregroundPhotoActive = false;
            return;
        }
        if (_hwnd == IntPtr.Zero)
        {
            _foregroundPhotoActive = false;
            return;
        }
        var foreground = Interop.NativeMethods.GetForegroundWindow();
        if (foreground != _hwnd)
        {
            _foregroundPhotoActive = false;
            return;
        }
        if (_foregroundPhotoActive)
        {
            return;
        }
        _foregroundPhotoActive = true;
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoForegroundDetected?.Invoke(),
            ex => Debug.WriteLine($"[PhotoForegroundDetected] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private void MonitorPresentationFocus()
    {
        DetectForegroundPresentation();
        DetectForegroundPhoto();
        var nowUtc = GetCurrentUtcTimestamp();
        if (!PresentationFocusMonitorPolicy.ShouldAttemptRestore(
                restoreEnabled: _presentationFocusRestoreEnabled,
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive(),
                foregroundOwnedByCurrentProcess: IsForegroundOwnedByCurrentProcess(),
                nowUtc: nowUtc,
                nextAttemptUtc: _nextPresentationFocusAttempt))
        {
            return;
        }
        var restored = RestorePresentationFocusIfNeeded(requireFullscreen: true);
        if (restored)
        {
            _nextPresentationFocusAttempt = PresentationFocusMonitorPolicy.ComputeNextAttemptUtc(
                nowUtc,
                PresentationFocusCooldownMs);
            LogPresentationState("focus-restored");
        }
    }

    private void MonitorInkContext()
    {
        var monitorStart = Stopwatch.StartNew();
        bool uiThread = Dispatcher.CheckAccess();
        _pendingInkContextCheck = false;

        var allowPresentation = _presentationOptions.AllowOffice || _presentationOptions.AllowWps;
        var photoOrBoardActive = PhotoInteractionModePolicy.IsPhotoOrBoardActive(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive());
        if (!allowPresentation)
        {
            if (_presentationFullscreenActive)
            {
                _presentationFullscreenActive = false;
                ClearCurrentPresentationType();
                SafeActionExecutionExecutor.TryExecute(
                    () => PresentationFullscreenDetected?.Invoke(),
                    ex => Debug.WriteLine($"[PresentationFullscreenDetected] callback failed: {ex.GetType().Name} - {ex.Message}"));
                if (!photoOrBoardActive)
                {
                    _currentCacheScope = InkCacheScope.None;
                    _currentCacheKey = string.Empty;
                    ClearInkSurfaceForPresentationExit();
                }
            }
        }
        else
        {
            UpdatePresentationFullscreenState(clearInkOnExit: !photoOrBoardActive);
        }

        if (photoOrBoardActive)
        {
            _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
            return;
        }
        if (ShouldDeferInkContext())
        {
            _pendingInkContextCheck = true;
            _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
            return;
        }

        UpdateInkMonitorInterval();
        _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
    }

    private void UpdatePresentationFullscreenState(bool clearInkOnExit)
    {
        // Keep fullscreen tracking aligned with slideshow-window validation.
        // This avoids treating WPS non-slideshow fullscreen windows as active presentation sessions.
        var nextType = ResolveFullscreenPresentationType();
        var fullscreenNow = nextType != PresentationType.None;
        var stateChanged = fullscreenNow != _presentationFullscreenActive;
        _presentationFullscreenActive = fullscreenNow;
        _currentPresentationType = fullscreenNow ? nextType : PresentationType.None;
        if (!stateChanged)
        {
            return;
        }
        if (fullscreenNow)
        {
            DispatchSessionEvent(new EnterPresentationFullscreenEvent(MapPresentationSource(nextType)));
        }
        else
        {
            DispatchSessionEvent(new ExitPresentationFullscreenEvent());
        }
        SafeActionExecutionExecutor.TryExecute(
            () => PresentationFullscreenDetected?.Invoke(),
            ex => Debug.WriteLine($"[PresentationFullscreenDetected] callback failed: {ex.GetType().Name} - {ex.Message}"));
        if (!fullscreenNow && clearInkOnExit)
        {
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceForPresentationExit();
        }
    }

    private PresentationType ResolveForegroundPresentationType()
    {
        var target = _presentationResolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            return PresentationType.None;
        }
        return _presentationClassifier.Classify(target.Info);
    }

    public void UpdateWpsMode(string mode)
    {
        _presentationInputPipeline.UpdateWpsMode(mode);
        _presentationOptions.Strategy = _presentationInputPipeline.WpsStrategy;
        WpsHookUnavailableNotificationPolicy.Reset(ref _wpsHookUnavailableNotifiedState);
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdateOfficeMode(string mode)
    {
        _presentationInputPipeline.UpdateOfficeMode(mode);
        UpdateFocusAcceptance();
    }

    public void UpdateWpsWheelMapping(bool enabled)
    {
        _presentationOptions.WheelAsKey = enabled;
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdateWpsDebounceMs(int debounceMs)
    {
        _presentationOptions.WpsDebounceMs = Math.Max(0, debounceMs);
    }

    public void UpdatePresentationDegradeLock(bool enabled)
    {
        _presentationOptions.LockStrategyWhenDegraded = enabled;
        if (!enabled)
        {
            _presentationInputPipeline.ResetAutoFallbacks();
        }
    }

    public void UpdatePresentationClassifierOverrides(string rawOverridesJson)
    {
        var hasParseError = false;
        if (!PresentationClassifierOverridesParser.TryParse(
                rawOverridesJson,
                out var overrides,
                out var error))
        {
            Debug.WriteLine($"[PresentationClassifier] overrides parse failed: {error}");
            overrides = PresentationClassifierOverrides.Empty;
            hasParseError = true;
        }

        if (!PresentationClassifierOverridesParser.TryParseScoringOptions(
                rawOverridesJson,
                out var scoringOptions,
                out var scoringError))
        {
            Debug.WriteLine($"[PresentationClassifier] scoring parse failed: {scoringError}");
            scoringOptions = PresentationWindowScoringOptions.Default;
            hasParseError = true;
        }

        _presentationClassifier = new PresentationClassifier(overrides);
        _presentationResolver.UpdateScoringOptions(scoringOptions);
        _presentationInputPipeline.ResetAutoFallbacks();
        if (hasParseError)
        {
            WpsHookUnavailableNotificationPolicy.Reset(ref _wpsHookUnavailableNotifiedState);
        }
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        UpdatePresentationFocusMonitor();
    }

    public void UpdatePresentationClassifierAutoLearn(bool enabled)
    {
        _presentationClassifierAutoLearnEnabled = enabled;
    }

    public bool TryBuildPresentationClassifierAutoLearnJson(
        string currentOverridesJson,
        out string mergedOverridesJson,
        out string reason)
    {
        mergedOverridesJson = currentOverridesJson ?? string.Empty;
        reason = string.Empty;
        if (!_presentationClassifierAutoLearnEnabled)
        {
            return false;
        }

        var foreground = _presentationResolver.ResolveForeground();
        if (!foreground.IsValid || foreground.Info == null)
        {
            return false;
        }

        var check = _presentationResolver.CheckWindow(foreground.Handle, _presentationClassifier);
        if (check == null || !check.IsFullscreen || check.ClassMatch)
        {
            return false;
        }

        if (!PresentationClassifierAutoLearnPolicy.TryBuildRequest(
                foreground.Info,
                check.Type,
                out var request))
        {
            return false;
        }
        if (!PresentationClassifierAutoLearnPolicy.TryMergeOverridesJson(
                currentOverridesJson,
                request,
                out mergedOverridesJson,
                out var error))
        {
            reason = $"merge-failed: {error}";
            return false;
        }
        if (string.Equals(mergedOverridesJson, currentOverridesJson, StringComparison.Ordinal))
        {
            return false;
        }

        reason =
            $"type={check.Type}; process={request.ProcessToken}; classes={string.Join("|", request.ClassTokens)}";
        return true;
    }

    public void UpdatePresentationTargets(bool allowOffice, bool allowWps)
    {
        _presentationOptions.AllowOffice = allowOffice;
        _presentationOptions.AllowWps = allowWps;
        if (!allowWps)
        {
            _presentationInputPipeline.ResetWpsHookFallback();
            WpsHookUnavailableNotificationPolicy.Reset(ref _wpsHookUnavailableNotifiedState);
        }
        _presentationInputPipeline.ResetOfficeAutoFallback();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        UpdatePresentationFocusMonitor();
    }

    public void UpdatePresentationForegroundPolicy(bool forceForegroundOnFullscreen)
    {
        _forcePresentationForegroundOnFullscreen = forceForegroundOnFullscreen;
    }

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
            if (TrySendPresentationCommandToTarget(target, command, options))
            {
                RememberWpsNav(direction, target.Handle);
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

    private bool TryHandlePresentationKey(Key key)
    {
        var presentationAllowed = _presentationOptions.AllowOffice || _presentationOptions.AllowWps;
        var keyMapped = PresentationKeyCommandPolicy.TryMap(key, out var command);
        if (!PresentationKeyboardDispatchPolicy.ShouldDispatch(presentationAllowed, keyMapped))
        {
            return false;
        }
        if (!TrySendPresentationCommand(command))
        {
            return false;
        }
        return true;
    }

    private bool TrySendPresentationCommand(ClassroomToolkit.Services.Presentation.PresentationCommand command)
    {
        return _presentationDispatchCoordinator.TryDispatch(
            allowOffice: _presentationOptions.AllowOffice,
            allowWps: _presentationOptions.AllowWps,
            currentPresentationType: _currentPresentationType,
            trySendWps: (target, allowBackground) => TrySendWpsNavigation(command, target, allowBackground),
            trySendOffice: (target, allowBackground) => TrySendOfficeNavigation(command, target, allowBackground));
    }

    private bool TrySendOfficeNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        PresentationTarget target,
        bool allowBackground)
    {
        if (!CanSendPresentationNavigation(
                allowChannel: _presentationOptions.AllowOffice,
                target,
                allowBackground))
        {
            return false;
        }
        var options = _presentationInputPipeline.BuildOfficeOptions(_presentationOptions);
        return TrySendPresentationCommandToTarget(target, command, options);
    }

    private bool TrySendPresentationCommandToTarget(
        PresentationTarget target,
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        ClassroomToolkit.Services.Presentation.PresentationControlOptions options)
    {
        if (!target.IsValid || options == null)
        {
            return false;
        }

        return _presentationService.TrySendToTarget(target, command, options);
    }

    private bool CanSendPresentationNavigation(
        bool allowChannel,
        PresentationTarget target,
        bool allowBackground)
    {
        var targetHasInfo = target.Info != null;
        var targetIsSlideshow = targetHasInfo && IsPresentationSlideshow(target);
        var targetForeground = target.IsValid && IsTargetForeground(target);
        return PresentationNavigationAdmissionPolicy.ShouldAttempt(
            allowChannel: allowChannel,
            boardActive: IsBoardActive(),
            targetIsValid: target.IsValid,
            targetHasInfo: targetHasInfo,
            targetIsSlideshow: targetIsSlideshow,
            allowBackground: allowBackground,
            targetForeground: targetForeground);
    }

    private bool IsPresentationSlideshow(PresentationTarget target)
    {
        return PresentationSlideshowDetectionPolicy.IsSlideshow(
            target,
            _presentationClassifier,
            IsFullscreenWindow);
    }

    private PresentationType ResolveFullscreenPresentationType()
    {
        bool wpsFullscreen = false;
        bool officeFullscreen = false;
        if (_presentationOptions.AllowWps)
        {
            var wpsTarget = ResolveWpsTarget();
            wpsFullscreen = IsFullscreenPresentationWindow(wpsTarget);
        }
        if (_presentationOptions.AllowOffice)
        {
            var officeTarget = _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId);
            officeFullscreen = IsFullscreenPresentationWindow(officeTarget);
        }
        return PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen,
            officeFullscreen,
            _currentPresentationType);
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
        return _presentationInputPipeline.ResolveWpsSendMode(target.IsValid);
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

    private bool IsTargetForeground(PresentationTarget target)
    {
        if (!target.IsValid)
        {
            return false;
        }
        return PresentationWindowFocus.IsForeground(target.Handle);
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

    private bool ShouldSuppressPresentationWheelFromRecentInkInput()
    {
        return PresentationWheelInkConflictPolicy.ShouldSuppress(
            _mode,
            _lastInkInputUtc,
            GetCurrentUtcTimestamp(),
            Math.Max(InkInputCooldownMs, WpsNavDebounceMs));
    }

    private bool IsForegroundOwnedByCurrentProcess()
    {
        var foreground = Interop.NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }
        var threadId = Interop.NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        if (threadId == 0 || processId == 0)
        {
            return false;
        }

        return processId == (uint)Environment.ProcessId;
    }

    private bool ShouldForcePresentationForeground(PresentationTarget target)
    {
        if (!_forcePresentationForegroundOnFullscreen)
        {
            return false;
        }
        // 特殊逻辑：仅当前进程正在主导交互时才强制抢占
        return IsFullscreenWindow(target.Handle);
    }

    private bool IsFullscreenPresentationWindow(PresentationTarget target)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }

        var fullscreen = IsFullscreenWindow(target.Handle);
        var slideshowClassMatch = _presentationClassifier.IsSlideshowWindow(target.Info);
        var classifiedType = _presentationClassifier.Classify(target.Info);
        return PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            target.IsValid,
            targetHasInfo: true,
            fullscreen,
            slideshowClassMatch,
            classifiesAsOffice: classifiedType == PresentationType.Office);
    }

    private bool IsFullscreenWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        var monitor = Interop.NativeMethods.MonitorFromWindow(hwnd, Interop.NativeMethods.MonitorDefaultToNearest);
        var info = new Interop.NativeMethods.MonitorInfo { Size = Marshal.SizeOf<Interop.NativeMethods.MonitorInfo>() };
        if (!Interop.NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }
        var rect = new Interop.NativeMethods.NativeRect();
        if (!Interop.NativeMethods.GetWindowRect(hwnd, out rect))
        {
            return false;
        }
        const int tolerance = PresentationFullscreenWindowDefaults.BoundsTolerancePixels;
        return Math.Abs(rect.Left - info.Monitor.Left) <= tolerance
               && Math.Abs(rect.Top - info.Monitor.Top) <= tolerance
               && Math.Abs(rect.Right - info.Monitor.Right) <= tolerance
               && Math.Abs(rect.Bottom - info.Monitor.Bottom) <= tolerance;
    }

    private void LogPresentationState(string reason)
    {
        Debug.WriteLine(
            $"[PresentationState] reason={reason}; allowWps={_presentationOptions.AllowWps}; allowOffice={_presentationOptions.AllowOffice}; " +
            $"wpsMode={_presentationOptions.Strategy}; officeMode={_presentationInputPipeline.OfficeStrategy}; wheelAsKey={_presentationOptions.WheelAsKey}; " +
            $"wpsDebounceMs={_presentationOptions.WpsDebounceMs}; lockOnDegrade={_presentationOptions.LockStrategyWhenDegraded}; " +
            $"hookActive={_wpsNavHookActive}; hookKeyboard={_wpsHookInterceptKeyboard}; hookWheel={_wpsHookInterceptWheel}; " +
            $"forceMessage={_presentationInputPipeline.WpsForceMessageFallback}; photoMode={_photoModeActive}; boardMode={IsBoardActive()}; " +
            $"fullscreen={_presentationFullscreenActive}; fgType={_foregroundPresentationType}; currentType={_currentPresentationType}");
    }

}

