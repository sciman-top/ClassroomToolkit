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
