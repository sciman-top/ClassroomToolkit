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
            && trySendWps(false))
        {
            return true;
        }

        if (context.ForegroundType == OverlayPresentationRouteType.Office
            && context.OfficeSlideshow
            && trySendOffice(false))
        {
            return true;
        }

        if (context.WpsSlideshow && context.OfficeSlideshow)
        {
            if (context.CurrentPresentationType == OverlayPresentationRouteType.Wps
                && trySendWps(true))
            {
                return true;
            }

            if (context.CurrentPresentationType == OverlayPresentationRouteType.Office
                && trySendOffice(true))
            {
                return true;
            }

            if (context.WpsFullscreen
                && !context.OfficeFullscreen
                && trySendWps(true))
            {
                return true;
            }

            if (context.OfficeFullscreen
                && !context.WpsFullscreen
                && trySendOffice(true))
            {
                return true;
            }
        }

        if (context.WpsSlideshow && trySendWps(true))
        {
            return true;
        }

        if (context.OfficeSlideshow && trySendOffice(true))
        {
            return true;
        }

        return false;
    }
}
