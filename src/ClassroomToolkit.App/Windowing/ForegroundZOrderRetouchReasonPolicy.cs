namespace ClassroomToolkit.App.Windowing;

internal static class ForegroundZOrderRetouchReasonPolicy
{
    internal static string ResolveTag(ForegroundZOrderRetouchReason reason)
    {
        return reason switch
        {
            ForegroundZOrderRetouchReason.ForceDisabledByDesign => "force-disabled-by-design",
            ForegroundZOrderRetouchReason.OverlayVisiblePresentation => "overlay-visible-presentation",
            ForegroundZOrderRetouchReason.OverlayHiddenPresentation => "overlay-hidden-presentation",
            _ => "none"
        };
    }
}
