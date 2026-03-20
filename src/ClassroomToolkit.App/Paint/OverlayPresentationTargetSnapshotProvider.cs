using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.App.Windowing;
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

    public OverlayPresentationTargetSnapshotProvider(
        IPresentationTargetResolver resolver,
        Func<PresentationClassifier> classifierAccessor,
        Func<IntPtr, bool> isFullscreenWindow,
        uint currentProcessId)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _classifierAccessor = classifierAccessor ?? throw new ArgumentNullException(nameof(classifierAccessor));
        _isFullscreenWindow = isFullscreenWindow ?? throw new ArgumentNullException(nameof(isFullscreenWindow));
        _currentProcessId = currentProcessId == 0
            ? (uint)Environment.ProcessId
            : currentProcessId;
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
        var wpsTarget = allowWps
            ? _resolver.ResolvePresentationTarget(
                classifier,
                allowWps: true,
                allowOffice: false,
                _currentProcessId)
            : PresentationTarget.Empty;
        var officeTarget = allowOffice
            ? _resolver.ResolvePresentationTarget(
                classifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId)
            : PresentationTarget.Empty;
        var wpsAnalysis = AnalyzeTarget(wpsTarget, classifier);
        var officeAnalysis = AnalyzeTarget(officeTarget, classifier);
        var foregroundType = ResolveForegroundPresentationType(classifier);

        return new OverlayPresentationTargetSnapshot(
            WpsTarget: wpsTarget,
            OfficeTarget: officeTarget,
            WpsSlideshow: wpsAnalysis.IsSlideshow,
            OfficeSlideshow: officeAnalysis.IsSlideshow,
            WpsFullscreen: wpsAnalysis.IsFullscreen,
            OfficeFullscreen: officeAnalysis.IsFullscreen,
            ForegroundType: foregroundType);
    }

    private (bool IsSlideshow, bool IsFullscreen) AnalyzeTarget(PresentationTarget target, PresentationClassifier classifier)
    {
        if (!target.IsValid || target.Info == null)
        {
            return (false, false);
        }

        var isFullscreen = _isFullscreenWindow(target.Handle);
        var isSlideshow = PresentationSlideshowDetectionPolicy.IsSlideshow(target, classifier, _ => isFullscreen);
        return (isSlideshow, isFullscreen);
    }

    private PresentationType ResolveForegroundPresentationType(PresentationClassifier classifier)
    {
        var target = _resolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            return PresentationType.None;
        }

        return classifier.Classify(target.Info);
    }
}
