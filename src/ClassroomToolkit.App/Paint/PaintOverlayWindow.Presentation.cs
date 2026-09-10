using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void ClearCurrentPresentationType()
    {
        _currentPresentationType = PresentationType.None;
    }

    private PresentationTarget ResolvePresentationTargetForChannel(PresentationType type)
    {
        return type switch
        {
            PresentationType.Wps => ResolveWpsTarget(),
            PresentationType.Office => ResolveOfficeTarget(),
            _ => PresentationTarget.Empty
        };
    }

    private PresentationTarget ResolveOfficeTarget()
    {
        return _presentationTargetSessionBinding.Resolve(
            PresentationType.Office,
            resolveCandidate: () => _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId),
            isAdmitted: target => _presentationTargetAdmission(target, PresentationType.Office));
    }

    private PresentationTarget ResolvePresentationFocusTarget(out PresentationType selectedType)
    {
        var foreground = _presentationResolver.ResolveForeground();
        var foregroundHasInfo = foreground.IsValid && foreground.Info != null;
        var foregroundType = foregroundHasInfo
            ? _presentationClassifier.Classify(foreground.Info!)
            : PresentationType.None;
        var foregroundIsFullscreen = foregroundHasInfo
                                     && IsFullscreenPresentationWindow(foreground);
        selectedType = PresentationTargetChannelSelectionPolicy.ResolveForFocus(
            foregroundType,
            foregroundIsFullscreen,
            _currentPresentationType,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice);
        return ResolvePresentationTargetForChannel(selectedType);
    }

    public bool RestorePresentationFocusIfNeeded(bool requireFullscreen = false)
    {
        var sessionState = _sessionCoordinator.CurrentState;
        var presentationAllowed = PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
            _presentationOptions.AllowOffice,
            _presentationOptions.AllowWps);
        var target = ResolvePresentationFocusTarget(out var targetType);
        var targetIsValid = target.IsValid;
        var targetIsSlideshow = targetIsValid && IsPresentationSlideshow(target, targetType);
        var targetIsFullscreen = targetIsValid && IsFullscreenPresentationWindow(target, targetType);
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
                foregroundOwned,
                WindowDragOperationState.IsActive))
        {
            return false;
        }

        var restored = PresentationForegroundSuppressionInteropAdapter.EnsureForeground(target.Handle);
        RequestPresentationOverlayRetouchIfNeeded(restored, "presentation-focus-restore");
        return restored;
    }

    public bool ForwardKeyboardToPresentation(Key key)
    {
        if (!PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
                _presentationOptions.AllowOffice,
                _presentationOptions.AllowWps))
        {
            return false;
        }
        if (!PresentationKeyCommandPolicy.TryMap(key, out var command))
        {
            return false;
        }
        return TrySendPresentationCommand(command);
    }

    public bool ForwardWheelToPresentation(int delta)
    {
        if (!PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
                _presentationOptions.AllowOffice,
                _presentationOptions.AllowWps))
        {
            return false;
        }
        if (ShouldSuppressPresentationWheelFromRecentInkInput())
        {
            return false;
        }

        var foregroundType = ResolveForegroundPresentationType();
        var executionAction = OverlayWheelPresentationExecutionPolicy.Resolve(
            _wpsNavHookActive,
            _wpsHookInterceptWheel,
            _wpsHookBlockOnly,
            isWpsForeground: OverlayPresentationRouteContextBuilder.MapRouteType(foregroundType) == OverlayPresentationRouteType.Wps,
            WpsHookRecentlyFired(),
            delta);
        var command = executionAction switch
        {
            OverlayWheelPresentationExecutionAction.SendNext => ClassroomToolkit.Services.Presentation.PresentationCommand.Next,
            OverlayWheelPresentationExecutionAction.SendPrevious => ClassroomToolkit.Services.Presentation.PresentationCommand.Previous,
            _ => (ClassroomToolkit.Services.Presentation.PresentationCommand?)null
        };
        if (!command.HasValue)
        {
            return false;
        }

        return TrySendPresentationCommand(command.Value);
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
        if (!IsFullscreenPresentationWindow(target, type))
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
        var foreground = _presentationResolver.ResolveForeground();
        if (!foreground.IsValid || foreground.Handle != _hwnd)
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
            TryFollowPresentationMonitor();
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
        if (!TryGetCurrentPresentationWindowCheck(target, expectedType: null, out var check))
        {
            return PresentationType.None;
        }

        return check!.ClassMatch || check.IsFullscreen
            ? check.Type
            : PresentationType.None;
    }

    private void TryFollowPresentationMonitor()
    {
        if (!PresentationFollowMonitorPolicy.ShouldFollow(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive(),
                overlayVisible: IsVisible,
                windowStateMinimized: WindowState == WindowState.Minimized))
        {
            return;
        }
        var target = ResolvePresentationTargetForChannel(_currentPresentationType);
        if (!IsFullscreenPresentationWindow(target, _currentPresentationType))
        {
            return;
        }
        var targetMonitorRect = GetMonitorRectOfWindow(target.Handle);
        if (!PresentationFollowMonitorPolicy.ShouldMove(
                GetCurrentMonitorRect(useWorkArea: false),
                targetMonitorRect))
        {
            return;
        }
        var hwnd = ResolveOverlayWindowHandle();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }
        NormalizeOverlayWindowState(shouldNormalize: true);
        // 与图片全屏同路：设备像素 SetWindowPos 保证真实铺满目标显示器。
        var positioned = WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(
            hwnd,
            (int)Math.Round(targetMonitorRect.Left, MidpointRounding.AwayFromZero),
            (int)Math.Round(targetMonitorRect.Top, MidpointRounding.AwayFromZero),
            (int)Math.Round(targetMonitorRect.Width, MidpointRounding.AwayFromZero),
            (int)Math.Round(targetMonitorRect.Height, MidpointRounding.AwayFromZero),
            showWindow: true);
        if (positioned)
        {
            EnsureRasterSurface();
            LogPresentationState("presentation-monitor-follow");
        }
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

    public void UpdatePresentationAutoFallbackFailureThreshold(int threshold)
    {
        _presentationOptions.AutoFallbackFailureThreshold = Math.Clamp(threshold, min: 1, max: 10);
    }

    public void UpdatePresentationAutoFallbackProbeIntervalCommands(int interval)
    {
        _presentationOptions.AutoFallbackProbeIntervalCommands = Math.Clamp(interval, min: 1, max: 100);
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
        _presentationService.UpdateClassifier(_presentationClassifier);
        _presentationResolver.UpdateScoringOptions(scoringOptions);
        _presentationTargetSessionBinding.InvalidateAll();
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
        if (!allowOffice)
        {
            _presentationTargetSessionBinding.Invalidate(PresentationType.Office);
        }
        if (!allowWps)
        {
            _presentationTargetSessionBinding.Invalidate(PresentationType.Wps);
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

    private bool TryHandlePresentationKey(Key key)
    {
        var presentationAllowed = _presentationOptions.AllowOffice || _presentationOptions.AllowWps;
        var keyMapped = PresentationKeyCommandPolicy.TryMap(key, out var command);
        if (!presentationAllowed || !keyMapped)
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
        var sent = _presentationDispatchCoordinator.TryDispatch(
            allowOffice: _presentationOptions.AllowOffice,
            allowWps: _presentationOptions.AllowWps,
            currentPresentationType: _currentPresentationType,
            trySendWps: (target, allowBackground) => TrySendWpsNavigation(command, target, allowBackground),
            trySendOffice: (target, allowBackground) => TrySendOfficeNavigation(command, target, allowBackground));
        RequestPresentationOverlayRetouchIfNeeded(sent, $"presentation-command:{command}");
        return sent;
    }

    private bool TrySendOfficeNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        PresentationTarget target,
        bool allowBackground)
    {
        if (!CanSendPresentationNavigation(
                allowChannel: _presentationOptions.AllowOffice,
                target,
                allowBackground,
                expectedType: PresentationType.Office))
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
        bool allowBackground,
        PresentationType expectedType)
    {
        var targetHasInfo = target.Info != null;
        var targetIsSlideshow = targetHasInfo && IsPresentationSlideshow(target, expectedType);
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

    private bool IsPresentationSlideshow(
        PresentationTarget target,
        PresentationType? expectedType = null)
    {
        if (!TryGetCurrentPresentationWindowCheck(target, expectedType, out var check))
        {
            return false;
        }

        // BuildWindowCheck already contains the current class/fullscreen facts;
        // never derive slideshow admission from the cached target metadata.
        return check!.ClassMatch || check.IsFullscreen;
    }

    private PresentationType ResolveFullscreenPresentationType()
    {
        var foreground = _presentationResolver.ResolveForeground();
        var foregroundHasInfo = foreground.IsValid && foreground.Info != null;
        var foregroundType = foregroundHasInfo
            ? _presentationClassifier.Classify(foreground.Info!)
            : PresentationType.None;
        var foregroundIsFullscreen = foregroundHasInfo && IsFullscreenWindow(foreground.Handle);
        var foregroundOwnedByCurrentProcess = foregroundHasInfo && foreground.Info!.ProcessId == _currentProcessId;

        bool wpsFullscreen = false;
        bool officeFullscreen = false;
        if (_presentationOptions.AllowWps)
        {
            var wpsTarget = ResolveWpsTarget();
            var hasFullscreenCandidate = IsFullscreenPresentationWindow(wpsTarget, PresentationType.Wps);
            wpsFullscreen = WpsFullscreenExitPolicy.ShouldTreatAsActiveFullscreen(
                hasFullscreenCandidate,
                foregroundType,
                foregroundIsFullscreen,
                foregroundOwnedByCurrentProcess);
        }
        if (_presentationOptions.AllowOffice)
        {
            var officeTarget = ResolveOfficeTarget();
            officeFullscreen = IsFullscreenPresentationWindow(officeTarget, PresentationType.Office);
        }
        return PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen,
            officeFullscreen,
            _currentPresentationType,
            foregroundType,
            foregroundIsFullscreen);
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
        var foreground = _presentationResolver.ResolveForeground();
        if (!foreground.IsValid || foreground.Info == null)
        {
            return false;
        }
        return foreground.Info.ProcessId == _currentProcessId;
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

    private bool IsFullscreenPresentationWindow(
        PresentationTarget target,
        PresentationType? expectedType = null)
    {
        if (!TryGetCurrentPresentationWindowCheck(target, expectedType, out var check))
        {
            return false;
        }

        var classifiedType = check!.Type;
        var dedicatedWpsRuntime = classifiedType == PresentationType.Wps
                                  && WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(
                                      check.ProcessName);
        return PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            target.IsValid,
            targetHasInfo: target.Info != null,
            check.IsFullscreen,
            check.ClassMatch,
            classifiesAsOffice: classifiedType == PresentationType.Office,
            classifiesAsDedicatedWpsRuntime: dedicatedWpsRuntime);
    }

    private bool TryGetCurrentPresentationWindowCheck(
        PresentationTarget target,
        PresentationType? expectedType,
        out PresentationWindowCheck? check)
    {
        check = null;
        if (!target.IsValid
            || target.Info == null
            || !PresentationWindowFocus.IsWindowValid(target.Handle))
        {
            return false;
        }

        check = _presentationResolver.CheckWindow(target.Handle, _presentationClassifier);
        return check != null
               && (!expectedType.HasValue || check.Type == expectedType.Value);
    }

    private bool IsFullscreenWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        var check = _presentationResolver.CheckWindow(hwnd, _presentationClassifier);
        return check?.IsFullscreen == true;
    }

    private void LogPresentationState(string reason)
    {
        Debug.WriteLine(
            $"[PresentationState] reason={reason}; allowWps={_presentationOptions.AllowWps}; allowOffice={_presentationOptions.AllowOffice}; " +
            $"wpsMode={_presentationOptions.Strategy}; officeMode={_presentationInputPipeline.OfficeStrategy}; wheelAsKey={_presentationOptions.WheelAsKey}; " +
            $"wpsDebounceMs={_presentationOptions.WpsDebounceMs}; lockOnDegrade={_presentationOptions.LockStrategyWhenDegraded}; " +
            $"fallbackThreshold={_presentationOptions.AutoFallbackFailureThreshold}; fallbackProbe={_presentationOptions.AutoFallbackProbeIntervalCommands}; " +
            $"hookActive={_wpsNavHookActive}; hookKeyboard={_wpsHookInterceptKeyboard}; hookWheel={_wpsHookInterceptWheel}; " +
            $"forceMessage={_presentationInputPipeline.WpsForceMessageFallback}; photoMode={_photoModeActive}; boardMode={IsBoardActive()}; " +
            $"fullscreen={_presentationFullscreenActive}; fgType={_foregroundPresentationType}; currentType={_currentPresentationType}");
    }

    private void RequestPresentationOverlayRetouchIfNeeded(bool actionApplied, string reason)
    {
        if (!PresentationOverlayRetouchPolicy.ShouldRequest(
                actionApplied,
                IsVisible,
                _presentationFullscreenActive))
        {
            return;
        }

        SafeActionExecutionExecutor.TryExecute(
            () => FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(ForceEnforceZOrder: true)),
            ex => Debug.WriteLine($"[PresentationOverlayRetouch] callback failed reason={reason}: {ex.GetType().Name} - {ex.Message}"));
    }

    public void UpdateReservedPresentationNavigationKeys(
        bool rollCallGroupSwitchEnabled,
        string? rollCallGroupSwitchKey)
    {
        var reservedKeys = PresentationReservedNavigationKeyPolicy.ResolveRollCallGroupSwitchKeys(
            rollCallGroupSwitchEnabled,
            rollCallGroupSwitchKey);
        // orchestrator 缓存保留键，供禁用→重启用循环后回填；这里同时立即写入钩子侧，
        // 覆盖钩子当前已启用（无需重启）的场景。
        _wpsHookOrchestrator.SetReservedPresentationKeys(reservedKeys);
        _wpsNavHookClient?.SetSuppressedKeyboardKeys(reservedKeys);
    }

}
