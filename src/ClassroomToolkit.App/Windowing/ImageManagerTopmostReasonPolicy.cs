namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerTopmostReasonPolicy
{
    internal static string ResolveTag(ImageManagerTopmostReason reason)
    {
        return reason switch
        {
            ImageManagerTopmostReason.ImageManagerHidden => "image-manager-hidden",
            ImageManagerTopmostReason.FrontSurfaceMismatch => "front-surface-mismatch",
            _ => "none"
        };
    }
}
