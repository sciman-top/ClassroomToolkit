using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarPassthroughActivationPolicyTests
{
    [Fact]
    public void ShouldReplayToolbarClick_ShouldReturnTrue_WhenRegionCaptureWasCanceledByToolbarPassthrough()
    {
        var shouldReplay = ToolbarPassthroughActivationPolicy.ShouldReplayToolbarClick(
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
        var shouldReplay = ToolbarPassthroughActivationPolicy.ShouldReplayToolbarClick(
            (RegionScreenCaptureCancelReason)cancelReason,
            (RegionScreenCapturePassthroughInputKind)passthroughInputKind,
            toolbarVisible);

        shouldReplay.Should().BeFalse();
    }

    [Fact]
    public void ShouldReplayToolbarClick_ShouldReturnFalse_WhenToolbarAlreadyHandledPress()
    {
        var shouldReplay = ToolbarPassthroughActivationPolicy.ShouldReplayToolbarClick(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.ToolbarHandledPress,
            toolbarVisible: true);

        shouldReplay.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnFalse_WhenToolbarClickWasReplayed()
    {
        var shouldArm = ToolbarPassthroughActivationPolicy.ShouldArmDirectWhiteboardEntry(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.PointerPress,
            toolbarClickReplayed: true);

        shouldArm.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnFalse_WhenToolbarAlreadyHandledPress()
    {
        var shouldArm = ToolbarPassthroughActivationPolicy.ShouldArmDirectWhiteboardEntry(
            RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
            RegionScreenCapturePassthroughInputKind.ToolbarHandledPress,
            toolbarClickReplayed: false);

        shouldArm.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmDirectWhiteboardEntry_ShouldReturnTrue_WhenPointerOnlyMovedIntoToolbar()
    {
        var shouldArm = ToolbarPassthroughActivationPolicy.ShouldArmDirectWhiteboardEntry(
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
        var shouldArm = ToolbarPassthroughActivationPolicy.ShouldArmDirectWhiteboardEntry(
            (RegionScreenCaptureCancelReason)cancelReason,
            (RegionScreenCapturePassthroughInputKind)passthroughInputKind,
            toolbarClickReplayed);

        shouldArm.Should().BeFalse();
    }
}
