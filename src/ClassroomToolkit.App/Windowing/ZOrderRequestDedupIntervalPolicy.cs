namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestDedupIntervalPolicy
{
    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive)
    {
        if (overlayVisible && (photoModeActive || whiteboardActive))
        {
            // Photo/whiteboard toolbar interactions can emit bursty requests.
            // Use a slightly wider dedup window to reduce visual retouch jitter.
            return ZOrderRequestDedupIntervalDefaults.InteractiveMs;
        }

        return ZOrderRequestBurstThresholds.RequestDedupMs;
    }
}
