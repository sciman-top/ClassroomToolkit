using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

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
