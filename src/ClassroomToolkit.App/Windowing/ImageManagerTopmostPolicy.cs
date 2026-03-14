namespace ClassroomToolkit.App.Windowing;

internal enum ImageManagerTopmostReason
{
    None = 0,
    ImageManagerHidden = 1,
    FrontSurfaceMismatch = 2
}

internal readonly record struct ImageManagerTopmostDecision(
    bool ShouldApply,
    ImageManagerTopmostReason Reason);

internal static class ImageManagerTopmostPolicy
{
    internal static ImageManagerTopmostDecision Resolve(bool imageManagerVisible, ZOrderSurface frontSurface)
    {
        if (!imageManagerVisible)
        {
            return new ImageManagerTopmostDecision(
                ShouldApply: false,
                Reason: ImageManagerTopmostReason.ImageManagerHidden);
        }

        return frontSurface == ZOrderSurface.ImageManager
            ? new ImageManagerTopmostDecision(
                ShouldApply: true,
                Reason: ImageManagerTopmostReason.None)
            : new ImageManagerTopmostDecision(
                ShouldApply: false,
                Reason: ImageManagerTopmostReason.FrontSurfaceMismatch);
    }

    internal static bool ShouldApply(bool imageManagerVisible, ZOrderSurface frontSurface)
    {
        return Resolve(imageManagerVisible, frontSurface).ShouldApply;
    }
}
