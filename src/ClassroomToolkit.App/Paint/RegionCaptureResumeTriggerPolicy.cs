namespace ClassroomToolkit.App.Paint;

internal readonly record struct RegionCaptureResumeTriggerDecision(
    bool ShouldClearDirectWhiteboardEntryArm,
    bool ShouldResumeRegionCapture);

internal static class RegionCaptureResumeTriggerPolicy
{
    internal static RegionCaptureResumeTriggerDecision Resolve(
        bool resumeArmed,
        bool toolbarVisible,
        bool toolbarLoaded,
        bool boardActive,
        bool overlayWhiteboardActive,
        bool pointerInsideToolbar)
    {
        if (!resumeArmed || !toolbarVisible || !toolbarLoaded)
        {
            return new RegionCaptureResumeTriggerDecision(
                ShouldClearDirectWhiteboardEntryArm: false,
                ShouldResumeRegionCapture: false);
        }

        if (boardActive || overlayWhiteboardActive)
        {
            return new RegionCaptureResumeTriggerDecision(
                ShouldClearDirectWhiteboardEntryArm: true,
                ShouldResumeRegionCapture: false);
        }

        if (pointerInsideToolbar)
        {
            return new RegionCaptureResumeTriggerDecision(
                ShouldClearDirectWhiteboardEntryArm: false,
                ShouldResumeRegionCapture: false);
        }

        return new RegionCaptureResumeTriggerDecision(
            ShouldClearDirectWhiteboardEntryArm: false,
            ShouldResumeRegionCapture: true);
    }
}
