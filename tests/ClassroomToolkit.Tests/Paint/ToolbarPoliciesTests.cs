using AwesomeAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests.Paint;

public sealed class ToolbarBoardClickActionPolicyTests
{
    [Fact]
    public void Resolve_ShouldOpenActionsPopup_WhenPhotoModeIsActiveAndWhiteboardIsInactive()
    {
        var action = ToolbarPolicies.ResolveBoardClickAction(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: false,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: false,
            photoModeActive: true);

        action.Should().Be(ToolbarBoardClickAction.OpenActionsPopup);
    }

    [Fact]
    public void Resolve_ShouldEnterWhiteboard_WhenPendingCaptureIsRetriedOutsidePhotoMode()
    {
        var action = ToolbarPolicies.ResolveBoardClickAction(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: false,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: true,
            photoModeActive: false);

        action.Should().Be(ToolbarBoardClickAction.EnterWhiteboard);
    }

    [Fact]
    public void Resolve_ShouldOpenActionsPopup_WhenPendingCaptureIsInPhotoMode()
    {
        var action = ToolbarPolicies.ResolveBoardClickAction(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: false,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: true,
            photoModeActive: true);

        action.Should().Be(ToolbarBoardClickAction.OpenActionsPopup);
    }

    [Fact]
    public void Resolve_ShouldExitExistingWhiteboardBeforeOpeningPopup()
    {
        var action = ToolbarPolicies.ResolveBoardClickAction(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: true,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: false,
            photoModeActive: true);

        action.Should().Be(ToolbarBoardClickAction.ExitWhiteboard);
    }
}

public sealed class ToolbarBoardSelectionVisualPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    public void Resolve_ShouldReturnFalse_WhenWhiteboardIsNotActuallyActive(
        bool boardActive,
        bool overlayWhiteboardActive,
        bool sessionCaptureWhiteboardActive,
        bool directWhiteboardEntryArmed)
    {
        var selected = ToolbarPolicies.ResolveBoardSelectionVisual(
            boardActive,
            overlayWhiteboardActive,
            sessionCaptureWhiteboardActive,
            directWhiteboardEntryArmed,
            regionCapturePending: false);

        selected.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Resolve_ShouldReturnTrue_WhenAnyWhiteboardSceneIsActuallyActive(
        bool boardActive,
        bool overlayWhiteboardActive,
        bool sessionCaptureWhiteboardActive)
    {
        var selected = ToolbarPolicies.ResolveBoardSelectionVisual(
            boardActive,
            overlayWhiteboardActive,
            sessionCaptureWhiteboardActive,
            directWhiteboardEntryArmed: false,
            regionCapturePending: false);

        selected.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenDirectWhiteboardEntryIsArmed()
    {
        var selected = ToolbarPolicies.ResolveBoardSelectionVisual(
            boardActive: false,
            overlayWhiteboardActive: false,
            sessionCaptureWhiteboardActive: false,
            directWhiteboardEntryArmed: true,
            regionCapturePending: false);

        selected.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenRegionCaptureIsPending()
    {
        var selected = ToolbarPolicies.ResolveBoardSelectionVisual(
            boardActive: false,
            overlayWhiteboardActive: false,
            sessionCaptureWhiteboardActive: false,
            directWhiteboardEntryArmed: false,
            regionCapturePending: true);

        selected.Should().BeTrue();
    }
}

public sealed class ToolbarPassthroughActivationPolicyTests
{
    [Fact]
    public void ShouldReplayToolbarClick_ShouldReturnTrue_WhenRegionCaptureWasCanceledByToolbarPassthrough()
    {
        var shouldReplay = ToolbarPolicies.ShouldReplayToolbarClick(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.PointerPress,
            toolbarVisible: true);

        shouldReplay.Should().BeTrue();
    }

    [Theory]
    [InlineData((int)RegionScreenCaptureCancelReason.UserCanceled, (int)RegionScreenCapturePassthroughInputKind.PointerPress, true)]
    [InlineData((int)RegionScreenCaptureCancelReason.None, (int)RegionScreenCapturePassthroughInputKind.PointerPress, true)]
    [InlineData((int)RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled, (int)RegionScreenCapturePassthroughInputKind.PointerMove, true)]
    [InlineData((int)RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled, (int)RegionScreenCapturePassthroughInputKind.None, true)]
    [InlineData((int)RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled, (int)RegionScreenCapturePassthroughInputKind.PointerPress, false)]
    public void ShouldReplayToolbarClick_ShouldReturnFalse_WhenCancelIsNotAVisibleToolbarClick(
        int cancelReason,
        int passthroughInputKind,
        bool toolbarVisible)
    {
        var shouldReplay = ToolbarPolicies.ShouldReplayToolbarClick(
            (RegionScreenCaptureCancelReason)cancelReason,
            (RegionScreenCapturePassthroughInputKind)passthroughInputKind,
            toolbarVisible);

        shouldReplay.Should().BeFalse();
    }

    [Fact]
    public void ShouldReplayToolbarClick_ShouldReturnFalse_WhenToolbarAlreadyHandledPress()
    {
        var shouldReplay = ToolbarPolicies.ShouldReplayToolbarClick(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.ToolbarHandledPress,
            toolbarVisible: true);

        shouldReplay.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnFalse_WhenToolbarClickWasReplayed()
    {
        var shouldArm = ToolbarPolicies.ShouldArmDirectWhiteboardEntry(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.PointerPress,
            toolbarClickReplayed: true);

        shouldArm.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnFalse_WhenToolbarAlreadyHandledPress()
    {
        var shouldArm = ToolbarPolicies.ShouldArmDirectWhiteboardEntry(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.ToolbarHandledPress,
            toolbarClickReplayed: false);

        shouldArm.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnTrue_WhenPointerOnlyMovedIntoToolbar()
    {
        var shouldArm = ToolbarPolicies.ShouldArmDirectWhiteboardEntry(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.PointerMove,
            toolbarClickReplayed: false);

        shouldArm.Should().BeTrue();
    }

    [Theory]
    [InlineData((int)RegionScreenCaptureCancelReason.UserCanceled, (int)RegionScreenCapturePassthroughInputKind.PointerMove, false)]
    [InlineData((int)RegionScreenCaptureCancelReason.None, (int)RegionScreenCapturePassthroughInputKind.PointerMove, false)]
    [InlineData((int)RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled, (int)RegionScreenCapturePassthroughInputKind.None, false)]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnFalse_WhenCancelDoesNotRepresentResumeIntent(
        int cancelReason,
        int passthroughInputKind,
        bool toolbarClickReplayed)
    {
        var shouldArm = ToolbarPolicies.ShouldArmDirectWhiteboardEntry(
            (RegionScreenCaptureCancelReason)cancelReason,
            (RegionScreenCapturePassthroughInputKind)passthroughInputKind,
            toolbarClickReplayed);

        shouldArm.Should().BeFalse();
    }
}

public sealed class ToolbarResumeCancellationPolicyTests
{
    [Fact]
    public void ShouldCancelPendingResumeOnToolbarPress_ShouldReturnTrue_ForNonBoardButtonWhileResumeIsArmed()
    {
        var shouldCancel = ToolbarPolicies.ShouldCancelPendingResumeOnToolbarPress(
            resumeArmed: true,
            pressedToolbarButton: true,
            pressedBoardButton: false);

        shouldCancel.Should().BeTrue();
    }

    [Fact]
    public void ShouldCancelPendingResumeOnToolbarPress_ShouldReturnFalse_ForBoardButton()
    {
        var shouldCancel = ToolbarPolicies.ShouldCancelPendingResumeOnToolbarPress(
            resumeArmed: true,
            pressedToolbarButton: true,
            pressedBoardButton: true);

        shouldCancel.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ShouldCancelPendingResumeOnToolbarPress_ShouldReturnFalse_WhenNoPendingResumeOrNoButton(
        bool resumeArmed,
        bool pressedToolbarButton,
        bool pressedBoardButton)
    {
        var shouldCancel = ToolbarPolicies.ShouldCancelPendingResumeOnToolbarPress(
            resumeArmed,
            pressedToolbarButton,
            pressedBoardButton);

        shouldCancel.Should().BeFalse();
    }
}

public sealed class ToolbarSecondTapIntentPolicyTests
{
    [Theory]
    [InlineData(false, true, "QuickColor", "None")]
    [InlineData(true, false, "QuickColor", "None")]
    [InlineData(true, true, "QuickColor", "QuickColor")]
    [InlineData(true, true, "Shape", "Shape")]
    public void Resolve_ShouldOnlyOpenSecondaryAction_WhenItemIsAlreadySelected_AndSupportsIt(
        bool alreadySelected,
        bool supportsSecondaryAction,
        string requestedTarget,
        string expected)
    {
        ToolbarPolicies.ResolveSecondTapIntent(
            alreadySelected,
            supportsSecondaryAction,
            Enum.Parse<ToolbarSecondTapTarget>(requestedTarget)).Should().Be(Enum.Parse<ToolbarSecondTapTarget>(expected));
    }
}
