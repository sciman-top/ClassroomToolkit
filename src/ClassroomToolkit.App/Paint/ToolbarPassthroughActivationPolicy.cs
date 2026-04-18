namespace ClassroomToolkit.App.Paint;

internal static class ToolbarPassthroughActivationPolicy
{
    internal static bool ShouldReplayToolbarClick(
        RegionScreenCaptureCancelReason cancelReason,
        RegionScreenCapturePassthroughInputKind passthroughInputKind,
        bool toolbarVisible)
    {
        return cancelReason == RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled
            && passthroughInputKind == RegionScreenCapturePassthroughInputKind.PointerPress
            && toolbarVisible;
    }

    internal static bool ShouldArmDirectWhiteboardEntry(
        RegionScreenCaptureCancelReason cancelReason,
        RegionScreenCapturePassthroughInputKind passthroughInputKind,
        bool toolbarClickReplayed)
    {
        if (cancelReason != RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled || toolbarClickReplayed)
        {
            return false;
        }

        return passthroughInputKind == RegionScreenCapturePassthroughInputKind.PointerMove;
    }
}
