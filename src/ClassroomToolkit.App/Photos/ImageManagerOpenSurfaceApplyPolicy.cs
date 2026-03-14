namespace ClassroomToolkit.App.Photos;

internal static class ImageManagerOpenSurfaceApplyPolicy
{
    internal static bool ShouldApply(bool touchImageManagerSurface, bool requestZOrderApply)
    {
        return touchImageManagerSurface || requestZOrderApply;
    }
}
