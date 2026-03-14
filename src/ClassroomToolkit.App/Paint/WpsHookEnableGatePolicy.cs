namespace ClassroomToolkit.App.Paint;

internal static class WpsHookEnableGatePolicy
{
    internal static bool ShouldAttemptResolveTarget(
        bool allowWps,
        bool boardActive,
        bool overlayVisible,
        bool photoModeActive)
    {
        if (!allowWps || boardActive || !overlayVisible || photoModeActive)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldEnableWithTarget(
        bool shouldAttemptResolveTarget,
        bool targetValid,
        bool targetIsSlideshow)
    {
        if (!shouldAttemptResolveTarget)
        {
            return false;
        }

        return targetValid && targetIsSlideshow;
    }
}
