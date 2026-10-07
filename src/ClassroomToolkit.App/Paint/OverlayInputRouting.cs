using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics;
using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class DispatcherInvokeAvailabilityPolicy
{
    internal static bool CanBeginInvoke(bool hasShutdownStarted, bool hasShutdownFinished)
    {
        return !hasShutdownStarted && !hasShutdownFinished;
    }
}

internal static class OverlayFocusAcceptancePolicy
{
    internal static bool ShouldBlockFocus(
        UiNavigationMode navigationMode,
        bool inputPassthroughEnabled,
        PaintToolMode mode,
        bool photoModeActive,
        bool boardActive,
        bool presentationAllowed,
        bool presentationTargetValid,
        bool wpsRawTargetValid)
    {
        if (mode != PaintToolMode.Cursor)
        {
            return false;
        }

        if (photoModeActive || boardActive)
        {
            return false;
        }

        if (inputPassthroughEnabled)
        {
            return true;
        }

        if (!presentationAllowed)
        {
            return false;
        }

        if (!UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode))
        {
            return false;
        }

        return presentationTargetValid || wpsRawTargetValid;
    }
}

internal static class OverlayFocusResolverGatePolicy
{
    internal static bool ShouldResolvePresentationTarget(
        bool presentationAllowed,
        bool navigationAllowsPresentationInput)
    {
        return presentationAllowed && navigationAllowsPresentationInput;
    }
}

internal static class OverlayHitTestPolicy
{
    internal static bool ShouldEnableOverlayHitTest(
        PaintToolMode mode,
        bool photoModeActive,
        bool photoLoading)
    {
        if (photoLoading)
        {
            return false;
        }

        return mode != PaintToolMode.Cursor || photoModeActive;
    }
}

internal static class OverlayInputPassthroughDefaults
{
    internal const double OpacityEpsilon = 0.001;
}

internal static class OverlayInputPassthroughPolicy
{
    private const double OpacityEpsilon = OverlayInputPassthroughDefaults.OpacityEpsilon;

    internal static bool ShouldEnable(
        PaintToolMode mode,
        double boardOpacity,
        bool photoModeActive)
    {
        if (photoModeActive)
        {
            return false;
        }

        if (mode != PaintToolMode.Cursor)
        {
            return false;
        }

        return boardOpacity <= OpacityEpsilon;
    }
}

internal enum OverlayWheelInputRoute
{
    Ignore = 0,
    ConsumeForBoard = 1,
    HandlePhoto = 2,
    RoutePresentation = 3
}

internal enum OverlayKeyInputRoute
{
    Ignore = 0,
    Consume = 1,
    RoutePresentation = 2
}

internal static class OverlayInputRoutingPolicy
{
    internal static OverlayWheelInputRoute ResolveWheelRoute(
        bool boardActive,
        bool photoModeActive,
        bool canRoutePresentationInput,
        bool presentationChannelEnabled)
    {
        if (boardActive)
        {
            return OverlayWheelInputRoute.ConsumeForBoard;
        }

        if (photoModeActive)
        {
            return OverlayWheelInputRoute.HandlePhoto;
        }

        if (!canRoutePresentationInput || !presentationChannelEnabled)
        {
            return OverlayWheelInputRoute.Ignore;
        }

        return OverlayWheelInputRoute.RoutePresentation;
    }

    internal static OverlayKeyInputRoute ResolveKeyRoute(
        bool photoLoading,
        bool photoKeyHandled,
        bool photoOrBoardActive,
        bool canRoutePresentationInput)
    {
        if (photoLoading)
        {
            return OverlayKeyInputRoute.Consume;
        }

        if (photoKeyHandled)
        {
            return OverlayKeyInputRoute.Consume;
        }

        if (photoOrBoardActive || !canRoutePresentationInput)
        {
            return OverlayKeyInputRoute.Ignore;
        }

        return OverlayKeyInputRoute.RoutePresentation;
    }
}

internal enum OverlayPointerSourceGateDecision
{
    Continue = 0,
    Ignore = 1,
    Consume = 2
}

internal static class OverlayPointerSourceGatePolicy
{
    internal static OverlayPointerSourceGateDecision Resolve(
        bool photoLoading,
        bool ignoreFromPhotoControls)
    {
        if (photoLoading)
        {
            return OverlayPointerSourceGateDecision.Consume;
        }

        if (ignoreFromPhotoControls)
        {
            return OverlayPointerSourceGateDecision.Ignore;
        }

        return OverlayPointerSourceGateDecision.Continue;
    }
}

internal readonly record struct OverlayPointerSourceHandlingPlan(
    bool ShouldContinue,
    bool ShouldMarkHandled,
    bool ShouldHideEraserPreview);

internal static class OverlayPointerSourceHandlingPolicy
{
    internal static OverlayPointerSourceHandlingPlan Resolve(
        OverlayPointerSourceGateDecision gateDecision,
        bool hideEraserPreviewWhenBlocked)
    {
        bool shouldHide = hideEraserPreviewWhenBlocked &&
                          gateDecision != OverlayPointerSourceGateDecision.Continue;
        return gateDecision switch
        {
            OverlayPointerSourceGateDecision.Continue => new OverlayPointerSourceHandlingPlan(
                ShouldContinue: true,
                ShouldMarkHandled: false,
                ShouldHideEraserPreview: false),
            OverlayPointerSourceGateDecision.Consume => new OverlayPointerSourceHandlingPlan(
                ShouldContinue: false,
                ShouldMarkHandled: true,
                ShouldHideEraserPreview: shouldHide),
            _ => new OverlayPointerSourceHandlingPlan(
                ShouldContinue: false,
                ShouldMarkHandled: false,
                ShouldHideEraserPreview: shouldHide)
        };
    }
}

internal enum OverlayPresentationRouteType
{
    None = 0,
    Wps = 1,
    Office = 2
}

internal readonly record struct OverlayPresentationCommandRouteContext(
    OverlayPresentationRouteType ForegroundType,
    OverlayPresentationRouteType CurrentPresentationType,
    bool WpsSlideshow,
    bool OfficeSlideshow,
    bool WpsFullscreen,
    bool OfficeFullscreen);

internal static class OverlayPresentationCommandRouter
{
    internal static bool TrySend(
        OverlayPresentationCommandRouteContext context,
        Func<bool, bool> trySendWps,
        Func<bool, bool> trySendOffice)
    {
        ArgumentNullException.ThrowIfNull(trySendWps);
        ArgumentNullException.ThrowIfNull(trySendOffice);

        if (context.ForegroundType == OverlayPresentationRouteType.Wps
            && context.WpsSlideshow
            && TrySendSafe(trySendWps, false))
        {
            return true;
        }

        if (context.ForegroundType == OverlayPresentationRouteType.Wps
            && context.WpsSlideshow)
        {
            // A known foreground channel is authoritative.  Do not let a
            // failed send fall through to another application's slideshow.
            return false;
        }

        if (context.ForegroundType == OverlayPresentationRouteType.Office
            && context.OfficeSlideshow
            && TrySendSafe(trySendOffice, false))
        {
            return true;
        }

        if (context.ForegroundType == OverlayPresentationRouteType.Office
            && context.OfficeSlideshow)
        {
            // A known foreground channel is authoritative.  Do not let a
            // failed send fall through to another application's slideshow.
            return false;
        }

        if (context.WpsSlideshow && context.OfficeSlideshow)
        {
            if (context.CurrentPresentationType == OverlayPresentationRouteType.Wps)
            {
                return TrySendSafe(trySendWps, true);
            }

            if (context.CurrentPresentationType == OverlayPresentationRouteType.Office)
            {
                return TrySendSafe(trySendOffice, true);
            }

            if (context.WpsFullscreen
                && !context.OfficeFullscreen)
            {
                return TrySendSafe(trySendWps, true);
            }

            if (context.OfficeFullscreen
                && !context.WpsFullscreen)
            {
                return TrySendSafe(trySendOffice, true);
            }

            // Both channels are available, but no evidence identifies the
            // intended slideshow.  A deterministic WPS-first fallback would
            // turn an ambiguous input into a wrong-app page turn.
            return false;
        }

        if (context.WpsSlideshow && TrySendSafe(trySendWps, true))
        {
            return true;
        }

        if (context.OfficeSlideshow && TrySendSafe(trySendOffice, true))
        {
            return true;
        }

        return false;
    }

    private static bool TrySendSafe(Func<bool, bool> sender, bool allowBackground)
    {
        return SafeActionExecutionExecutor.TryExecute(
            () => sender(allowBackground),
            fallback: false);
    }
}

internal sealed class OverlayPresentationDispatchCoordinator
{
    private readonly IOverlayPresentationTargetSnapshotProvider _snapshotProvider;

    public OverlayPresentationDispatchCoordinator(IOverlayPresentationTargetSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    }

    public bool TryDispatch(
        bool allowOffice,
        bool allowWps,
        PresentationType currentPresentationType,
        Func<PresentationTarget, bool, bool> trySendWps,
        Func<PresentationTarget, bool, bool> trySendOffice)
    {
        ArgumentNullException.ThrowIfNull(trySendWps);
        ArgumentNullException.ThrowIfNull(trySendOffice);

        return SafeActionExecutionExecutor.TryExecute(
            () => TryDispatchCore(
                allowOffice,
                allowWps,
                currentPresentationType,
                trySendWps,
                trySendOffice),
            fallback: false,
            onFailure: ex => Debug.WriteLine($"[PresentationDispatch] coordinator failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private bool TryDispatchCore(
        bool allowOffice,
        bool allowWps,
        PresentationType currentPresentationType,
        Func<PresentationTarget, bool, bool> trySendWps,
        Func<PresentationTarget, bool, bool> trySendOffice)
    {
        if (!PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(allowOffice, allowWps))
        {
            return false;
        }

        var snapshot = _snapshotProvider.Resolve(allowWps, allowOffice);
        if (!snapshot.WpsSlideshow && !snapshot.OfficeSlideshow)
        {
            return false;
        }

        var context = OverlayPresentationRouteContextBuilder.Build(
            foregroundType: snapshot.ForegroundType,
            currentPresentationType: currentPresentationType,
            wpsSlideshow: snapshot.WpsSlideshow,
            officeSlideshow: snapshot.OfficeSlideshow,
            wpsFullscreen: snapshot.WpsFullscreen,
            officeFullscreen: snapshot.OfficeFullscreen);

        return OverlayPresentationCommandRouter.TrySend(
            context,
            allowBackground => TrySendWithValidTarget(snapshot.WpsTarget, allowBackground, trySendWps),
            allowBackground => TrySendWithValidTarget(snapshot.OfficeTarget, allowBackground, trySendOffice));
    }

    private static bool TrySendWithValidTarget(
        PresentationTarget target,
        bool allowBackground,
        Func<PresentationTarget, bool, bool> sender)
    {
        if (!target.IsValid)
        {
            return false;
        }

        return sender(target, allowBackground);
    }
}

internal static class OverlayPresentationRouteContextBuilder
{
    internal static OverlayPresentationCommandRouteContext Build(
        PresentationType foregroundType,
        PresentationType currentPresentationType,
        bool wpsSlideshow,
        bool officeSlideshow,
        bool wpsFullscreen,
        bool officeFullscreen)
    {
        var bothTargetsAvailable = wpsSlideshow && officeSlideshow;
        return new OverlayPresentationCommandRouteContext(
            ForegroundType: MapRouteType(foregroundType),
            CurrentPresentationType: MapRouteType(currentPresentationType),
            WpsSlideshow: wpsSlideshow,
            OfficeSlideshow: officeSlideshow,
            WpsFullscreen: bothTargetsAvailable && wpsFullscreen,
            OfficeFullscreen: bothTargetsAvailable && officeFullscreen);
    }

    internal static OverlayPresentationRouteType MapRouteType(PresentationType type)
    {
        return type switch
        {
            PresentationType.Wps => OverlayPresentationRouteType.Wps,
            PresentationType.Office => OverlayPresentationRouteType.Office,
            _ => OverlayPresentationRouteType.None
        };
    }
}

internal static class OverlayPresentationRoutingPolicy
{
    internal static bool CanRouteFromAuxWindow(
        UiNavigationMode navigationMode,
        bool photoModeActive,
        bool boardActive)
    {
        if (photoModeActive || boardActive)
        {
            return false;
        }

        return UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode);
    }

    internal static bool CanRouteFromOverlay(
        UiNavigationMode navigationMode,
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inputPassthroughEnabled)
    {
        if (photoModeActive || boardActive)
        {
            return false;
        }

        if (!UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode))
        {
            return false;
        }

        if (mode == PaintToolMode.Cursor && inputPassthroughEnabled)
        {
            return false;
        }

        return true;
    }
}

internal readonly record struct OverlayPresentationTargetSnapshot(
    PresentationTarget WpsTarget,
    PresentationTarget OfficeTarget,
    bool WpsSlideshow,
    bool OfficeSlideshow,
    bool WpsFullscreen,
    bool OfficeFullscreen,
    PresentationType ForegroundType);

internal interface IOverlayPresentationTargetSnapshotProvider
{
    OverlayPresentationTargetSnapshot Resolve(bool allowWps, bool allowOffice);
}

internal sealed class OverlayPresentationTargetSnapshotProvider : IOverlayPresentationTargetSnapshotProvider
{
    private static readonly OverlayPresentationTargetSnapshot EmptySnapshot = new(
        WpsTarget: PresentationTarget.Empty,
        OfficeTarget: PresentationTarget.Empty,
        WpsSlideshow: false,
        OfficeSlideshow: false,
        WpsFullscreen: false,
        OfficeFullscreen: false,
        ForegroundType: PresentationType.None);

    private readonly IPresentationTargetResolver _resolver;
    private readonly Func<PresentationClassifier> _classifierAccessor;
    private readonly Func<IntPtr, bool> _isFullscreenWindow;
    private readonly uint _currentProcessId;
    private readonly PresentationTargetSessionBinding _sessionBinding;
    private readonly Func<IntPtr, bool> _isWindowValid;
    private readonly Func<PresentationTarget, PresentationType, bool>? _targetAdmission;

    public OverlayPresentationTargetSnapshotProvider(
        IPresentationTargetResolver resolver,
        Func<PresentationClassifier> classifierAccessor,
        Func<IntPtr, bool> isFullscreenWindow,
        uint currentProcessId,
        PresentationTargetSessionBinding? sessionBinding = null,
        Func<IntPtr, bool>? isWindowValid = null,
        Func<PresentationTarget, PresentationType, bool>? targetAdmission = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _classifierAccessor = classifierAccessor ?? throw new ArgumentNullException(nameof(classifierAccessor));
        _isFullscreenWindow = isFullscreenWindow ?? throw new ArgumentNullException(nameof(isFullscreenWindow));
        _currentProcessId = currentProcessId == 0
            ? (uint)Environment.ProcessId
            : currentProcessId;
        _sessionBinding = sessionBinding ?? new PresentationTargetSessionBinding();
        _isWindowValid = isWindowValid ?? (hwnd => hwnd != IntPtr.Zero);
        _targetAdmission = targetAdmission;
    }

    public OverlayPresentationTargetSnapshot Resolve(bool allowWps, bool allowOffice)
    {
        return SafeActionExecutionExecutor.TryExecute(
            () => ResolveCore(allowWps, allowOffice),
            fallback: EmptySnapshot,
            onFailure: ex => Debug.WriteLine($"[PresentationSnapshot] resolve failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private OverlayPresentationTargetSnapshot ResolveCore(bool allowWps, bool allowOffice)
    {
        if (!allowWps && !allowOffice)
        {
            return EmptySnapshot;
        }

        var classifier = _classifierAccessor() ?? new PresentationClassifier();
        var fullscreenCache = new Dictionary<IntPtr, bool>();
        bool IsFullscreenCached(IntPtr hwnd)
        {
            if (!fullscreenCache.TryGetValue(hwnd, out var isFullscreen))
            {
                isFullscreen = _isFullscreenWindow(hwnd);
                fullscreenCache[hwnd] = isFullscreen;
            }

            return isFullscreen;
        }

        var foreground = _resolver.ResolveForeground();
        var wpsTarget = allowWps
            ? ResolveTarget(PresentationType.Wps, classifier, IsFullscreenCached, foreground)
            : PresentationTarget.Empty;
        var officeTarget = allowOffice
            ? ResolveTarget(PresentationType.Office, classifier, IsFullscreenCached, foreground)
            : PresentationTarget.Empty;
        var wpsAnalysis = AnalyzeTarget(
            wpsTarget,
            PresentationType.Wps,
            classifier,
            IsFullscreenCached);
        var officeAnalysis = AnalyzeTarget(
            officeTarget,
            PresentationType.Office,
            classifier,
            IsFullscreenCached);
        var foregroundType = ResolveForegroundPresentationType(foreground, classifier, IsFullscreenCached);

        return new OverlayPresentationTargetSnapshot(
            WpsTarget: wpsTarget,
            OfficeTarget: officeTarget,
            WpsSlideshow: wpsAnalysis.IsSlideshow,
            OfficeSlideshow: officeAnalysis.IsSlideshow,
            WpsFullscreen: wpsAnalysis.IsFullscreen,
            OfficeFullscreen: officeAnalysis.IsFullscreen,
            ForegroundType: foregroundType);
    }

    private PresentationTarget ResolveTarget(
        PresentationType type,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow,
        PresentationTarget foreground)
    {
        var preferredTarget = ResolvePreferredForegroundTarget(
            foreground,
            type,
            classifier,
            isFullscreenWindow);
        return _sessionBinding.Resolve(
            type,
            resolveCandidate: () => _resolver.ResolvePresentationTarget(
                classifier,
                allowWps: type == PresentationType.Wps,
                allowOffice: type == PresentationType.Office,
                _currentProcessId),
            isAdmitted: target => IsAdmittedTarget(
                target,
                type,
                classifier,
                isFullscreenWindow,
                _isWindowValid,
                _targetAdmission),
            preferredTarget: preferredTarget);
    }

    private PresentationTarget ResolvePreferredForegroundTarget(
        PresentationTarget foreground,
        PresentationType type,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow)
    {
        if (!foreground.IsValid
            || foreground.Info == null
            || classifier.Classify(foreground.Info) != type
            || !_isWindowValid(foreground.Handle))
        {
            return PresentationTarget.Empty;
        }

        if (_targetAdmission != null)
        {
            return _targetAdmission(foreground, type)
                ? foreground
                : PresentationTarget.Empty;
        }

        return PresentationSlideshowDetectionPolicy.IsSlideshow(
            foreground,
            classifier,
            isFullscreenWindow,
            type)
            ? foreground
            : PresentationTarget.Empty;
    }

    private static bool IsAdmittedTarget(
        PresentationTarget target,
        PresentationType type,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow,
        Func<IntPtr, bool> isWindowValid,
        Func<PresentationTarget, PresentationType, bool>? targetAdmission)
    {
        if (targetAdmission != null)
        {
            return targetAdmission(target, type);
        }

        if (!target.IsValid || target.Info == null || !isWindowValid(target.Handle))
        {
            return false;
        }

        if (classifier.Classify(target.Info) != type)
        {
            return false;
        }

        return PresentationSlideshowDetectionPolicy.IsSlideshow(
            target,
            classifier,
            isFullscreenWindow,
            type);
    }

    private static (bool IsSlideshow, bool IsFullscreen) AnalyzeTarget(
        PresentationTarget target,
        PresentationType type,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow)
    {
        if (!target.IsValid || target.Info == null)
        {
            return (false, false);
        }

        var isFullscreen = isFullscreenWindow(target.Handle);
        var isSlideshow = PresentationSlideshowDetectionPolicy.IsSlideshow(
            target,
            classifier,
            _ => isFullscreen,
            type);
        return (isSlideshow, isFullscreen);
    }

    private PresentationType ResolveForegroundPresentationType(
        PresentationTarget target,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow)
    {
        if (!target.IsValid || target.Info == null)
        {
            return PresentationType.None;
        }

        var type = classifier.Classify(target.Info);
        if (type is PresentationType.None or PresentationType.Other)
        {
            return PresentationType.None;
        }

        if (_targetAdmission != null)
        {
            return _targetAdmission(target, type) ? type : PresentationType.None;
        }

        if (!_isWindowValid(target.Handle))
        {
            return PresentationType.None;
        }

        return PresentationSlideshowDetectionPolicy.IsSlideshow(
                target,
                classifier,
                isFullscreenWindow,
                type)
            ? type
            : PresentationType.None;
    }
}

internal static class OverlayTopmostApplyGatePolicy
{
    internal static bool ShouldApply(bool overlayVisible, WindowState windowState)
    {
        return overlayVisible && windowState != WindowState.Minimized;
    }
}

internal enum OverlayWheelPresentationExecutionAction
{
    None = 0,
    SendNext = 1,
    SendPrevious = 2
}

internal static class OverlayWheelPresentationExecutionPolicy
{
    internal static OverlayWheelPresentationExecutionAction Resolve(
        bool hookActive,
        bool hookInterceptWheel,
        bool hookBlockOnly,
        bool isWpsForeground,
        bool hookRecentlyFired,
        int wheelDelta)
    {
        if (WpsWheelRoutingPolicy.ShouldBypassDirectSend(
                hookActive,
                hookInterceptWheel,
                hookBlockOnly,
                isWpsForeground))
        {
            return OverlayWheelPresentationExecutionAction.None;
        }

        if (hookRecentlyFired)
        {
            return OverlayWheelPresentationExecutionAction.None;
        }

        return wheelDelta < 0
            ? OverlayWheelPresentationExecutionAction.SendNext
            : OverlayWheelPresentationExecutionAction.SendPrevious;
    }
}

internal static class OverlayWindowStyleApplyPolicy
{
    internal static bool ShouldApply(
        bool inputPassthroughEnabled,
        bool focusBlocked,
        bool? lastInputPassthroughEnabled,
        bool? lastFocusBlocked)
    {
        return lastInputPassthroughEnabled != inputPassthroughEnabled
            || lastFocusBlocked != focusBlocked;
    }
}

internal static class OverlayWindowStyleBitsPolicy
{
    internal readonly record struct StyleMask(int SetMask, int ClearMask);

    internal static StyleMask Resolve(bool inputPassthroughEnabled, bool focusBlocked)
    {
        var setMask = 0;
        var clearMask = 0;

        if (inputPassthroughEnabled)
        {
            setMask |= WindowStyleBitMasks.WsExTransparent;
        }
        else
        {
            clearMask |= WindowStyleBitMasks.WsExTransparent;
        }

        if (focusBlocked)
        {
            setMask |= WindowStyleBitMasks.WsExNoActivate;
        }
        else
        {
            clearMask |= WindowStyleBitMasks.WsExNoActivate;
        }

        return new StyleMask(setMask, clearMask);
    }
}

internal static class PresentationChannelAvailabilityPolicy
{
    internal static bool IsAnyChannelEnabled(bool allowOffice, bool allowWps)
    {
        return allowOffice || allowWps;
    }
}
