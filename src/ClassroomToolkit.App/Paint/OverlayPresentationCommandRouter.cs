using ClassroomToolkit.App.Windowing;
namespace ClassroomToolkit.App.Paint;

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

        if (context.ForegroundType == OverlayPresentationRouteType.Office
            && context.OfficeSlideshow
            && TrySendSafe(trySendOffice, false))
        {
            return true;
        }

        if (context.WpsSlideshow && context.OfficeSlideshow)
        {
            if (context.CurrentPresentationType == OverlayPresentationRouteType.Wps
                && TrySendSafe(trySendWps, true))
            {
                return true;
            }

            if (context.CurrentPresentationType == OverlayPresentationRouteType.Office
                && TrySendSafe(trySendOffice, true))
            {
                return true;
            }

            if (context.WpsFullscreen
                && !context.OfficeFullscreen
                && TrySendSafe(trySendWps, true))
            {
                return true;
            }

            if (context.OfficeFullscreen
                && !context.WpsFullscreen
                && TrySendSafe(trySendOffice, true))
            {
                return true;
            }
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
