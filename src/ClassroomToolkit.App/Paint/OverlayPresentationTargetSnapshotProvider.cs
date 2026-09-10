using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics;

namespace ClassroomToolkit.App.Paint;

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

        var wpsTarget = allowWps
            ? ResolveTarget(PresentationType.Wps, classifier, IsFullscreenCached)
            : PresentationTarget.Empty;
        var officeTarget = allowOffice
            ? ResolveTarget(PresentationType.Office, classifier, IsFullscreenCached)
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
        var foregroundType = ResolveForegroundPresentationType(classifier, IsFullscreenCached);

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
        Func<IntPtr, bool> isFullscreenWindow)
    {
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
                _targetAdmission));
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
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow)
    {
        var target = _resolver.ResolveForeground();
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
