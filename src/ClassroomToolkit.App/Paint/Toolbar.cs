
namespace ClassroomToolkit.App.Paint;

internal enum ToolbarBoardClickAction
{
    OpenActionsPopup = 0,
    ExitSessionCaptureWhiteboard = 1,
    ExitWhiteboard = 2,
    EnterWhiteboard = 3
}

internal static class ToolbarBoardClickActionPolicy
{
    internal static ToolbarBoardClickAction Resolve(
        bool sessionCaptureWhiteboardActive,
        bool whiteboardActive,
        bool shouldEnterWhiteboardBySecondTap,
        bool directWhiteboardEntryArmed,
        bool resumeRegionCaptureArmed,
        bool regionCapturePending,
        bool photoModeActive)
    {
        if (sessionCaptureWhiteboardActive)
        {
            return ToolbarBoardClickAction.ExitSessionCaptureWhiteboard;
        }

        if (whiteboardActive)
        {
            return ToolbarBoardClickAction.ExitWhiteboard;
        }

        if (shouldEnterWhiteboardBySecondTap)
        {
            return ToolbarBoardClickAction.EnterWhiteboard;
        }

        if ((directWhiteboardEntryArmed || resumeRegionCaptureArmed || regionCapturePending)
            && !photoModeActive)
        {
            return ToolbarBoardClickAction.EnterWhiteboard;
        }

        return ToolbarBoardClickAction.OpenActionsPopup;
    }
}

internal static class ToolbarBoardSelectionVisualPolicy
{
    internal static bool Resolve(
        bool boardActive,
        bool overlayWhiteboardActive,
        bool sessionCaptureWhiteboardActive,
        bool directWhiteboardEntryArmed,
        bool regionCapturePending)
    {
        if (boardActive
            || overlayWhiteboardActive
            || sessionCaptureWhiteboardActive
            || directWhiteboardEntryArmed
            || regionCapturePending)
        {
            return true;
        }

        return false;
    }
}

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

internal static class ToolbarResumeCancellationPolicy
{
    internal static bool ShouldCancelPendingResumeOnToolbarPress(
        bool resumeArmed,
        bool pressedToolbarButton,
        bool pressedBoardButton)
    {
        return resumeArmed && pressedToolbarButton && !pressedBoardButton;
    }
}

internal static class ToolbarScaleDefaults
{
    internal const double Min = 0.8;
    internal const double Default = 1.0;
    internal const double Max = 2.0;
}

internal static class ToolbarSecondTapIntentPolicy
{
    internal static ToolbarSecondTapTarget Resolve(
        bool alreadySelected,
        bool supportsSecondaryAction,
        ToolbarSecondTapTarget requestedTarget)
    {
        return alreadySelected && supportsSecondaryAction
            ? requestedTarget
            : ToolbarSecondTapTarget.None;
    }
}

internal enum ToolbarSecondTapTarget
{
    None,
    QuickColor,
    Shape,
    Board
}
