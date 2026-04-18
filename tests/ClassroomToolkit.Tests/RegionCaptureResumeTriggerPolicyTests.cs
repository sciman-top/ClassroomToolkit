using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RegionCaptureResumeTriggerPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoAction_WhenResumeIsNotArmed()
    {
        var decision = RegionCaptureResumeTriggerPolicy.Resolve(
            resumeArmed: false,
            toolbarVisible: true,
            toolbarLoaded: true,
            boardActive: false,
            overlayWhiteboardActive: false,
            pointerInsideToolbar: false);

        decision.ShouldClearDirectWhiteboardEntryArm.Should().BeFalse();
        decision.ShouldResumeRegionCapture.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldClearArm_WhenBoardIsAlreadyActive()
    {
        var decision = RegionCaptureResumeTriggerPolicy.Resolve(
            resumeArmed: true,
            toolbarVisible: true,
            toolbarLoaded: true,
            boardActive: true,
            overlayWhiteboardActive: false,
            pointerInsideToolbar: false);

        decision.ShouldClearDirectWhiteboardEntryArm.Should().BeTrue();
        decision.ShouldResumeRegionCapture.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldResumeCapture_WhenPointerLeavesToolbar()
    {
        var decision = RegionCaptureResumeTriggerPolicy.Resolve(
            resumeArmed: true,
            toolbarVisible: true,
            toolbarLoaded: true,
            boardActive: false,
            overlayWhiteboardActive: false,
            pointerInsideToolbar: false);

        decision.ShouldClearDirectWhiteboardEntryArm.Should().BeFalse();
        decision.ShouldResumeRegionCapture.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepWaiting_WhenPointerStillInsideToolbar()
    {
        var decision = RegionCaptureResumeTriggerPolicy.Resolve(
            resumeArmed: true,
            toolbarVisible: true,
            toolbarLoaded: true,
            boardActive: false,
            overlayWhiteboardActive: false,
            pointerInsideToolbar: true);

        decision.ShouldClearDirectWhiteboardEntryArm.Should().BeFalse();
        decision.ShouldResumeRegionCapture.Should().BeFalse();
    }
}
