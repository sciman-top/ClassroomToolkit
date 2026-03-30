using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

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
