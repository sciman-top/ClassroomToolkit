namespace ClassroomToolkit.App.Photos;

internal static class ImageManagerStateChangeSurfaceApplyPolicy
{
    internal static bool ShouldApply(bool requestZOrderApply, bool forceEnforceZOrder)
    {
        return requestZOrderApply || forceEnforceZOrder;
    }
}
