using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Drawing;
using System.IO;

namespace ClassroomToolkit.Tests;

public sealed class RegionCaptureInitialPassthroughPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPointerMovePassthrough_WhenPointerStartsInsideToolbarRegion()
    {
        var decision = SceneResolversPolicies.ResolveRegionCaptureInitialPassthrough(
            pointerScreenX: 120,
            pointerScreenY: 80,
            passthroughRegions: new[] { new Rectangle(100, 60, 200, 48) });

        decision.ShouldCancel.Should().BeTrue();
        decision.InputKind.Should().Be(RegionScreenCapturePassthroughInputKind.PointerMove);
    }

    [Fact]
    public void Resolve_ShouldReturnNoPassthrough_WhenPointerStartsOutsideToolbarRegion()
    {
        var decision = SceneResolversPolicies.ResolveRegionCaptureInitialPassthrough(
            pointerScreenX: 90,
            pointerScreenY: 80,
            passthroughRegions: new[] { new Rectangle(100, 60, 200, 48) });

        decision.ShouldCancel.Should().BeFalse();
        decision.InputKind.Should().Be(RegionScreenCapturePassthroughInputKind.None);
    }

    [Fact]
    public void IsSessionRegionCaptureFilePath_ShouldReturnFalse_ForInvalidPathInput()
    {
        var result = RegionScreenCaptureWorkflow.IsSessionRegionCaptureFilePath("capture-\0bad.png");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsSessionRegionCaptureFilePath_ShouldRecognizeSessionCaptureFileUnderRoot()
    {
        var path = Path.Combine(
            RegionScreenCaptureWorkflow.GetSessionCaptureRootDirectory(),
            "capture-test.png");

        RegionScreenCaptureWorkflow.IsSessionRegionCaptureFilePath(path).Should().BeTrue();
    }

    [Fact]
    public void RegionCaptureWorkflow_ShouldCaptureOnlyTheSelectedTargetBitmap()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveAppPath(
            "Paint",
            "RegionScreenCaptureWorkflow.cs"));

        source.Should().Contain("new Bitmap(target.Width, target.Height, PixelFormat.Format32bppArgb)");
        source.Should().Contain("target.Left");
        source.Should().Contain("target.Top");
        source.Should().Contain("target.Size");
        source.Should().NotContain("new Bitmap(virtualBounds.Width, virtualBounds.Height");
        source.Should().NotContain("full.Clone(localRect");
    }
}

public sealed class RegionCaptureResumeTriggerPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoAction_WhenResumeIsNotArmed()
    {
        var decision = SceneResolversPolicies.ResolveRegionCaptureResumeTrigger(
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
        var decision = SceneResolversPolicies.ResolveRegionCaptureResumeTrigger(
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
        var decision = SceneResolversPolicies.ResolveRegionCaptureResumeTrigger(
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
        var decision = SceneResolversPolicies.ResolveRegionCaptureResumeTrigger(
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

public sealed class RegionSelectionCompletionPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3.9, 20)]
    [InlineData(20, 3.9)]
    public void ResolvePointerRelease_ShouldKeepWaiting_WhenSelectionIsTooSmall(double width, double height)
    {
        var decision = SceneResolversPolicies.ResolvePointerRelease(width, height);

        decision.Should().Be(RegionSelectionCompletionDecision.KeepWaiting);
    }

    [Fact]
    public void ResolvePointerRelease_ShouldAccept_WhenSelectionIsLargeEnough()
    {
        var decision = SceneResolversPolicies.ResolvePointerRelease(4, 4);

        decision.Should().Be(RegionSelectionCompletionDecision.Accept);
    }
}

public sealed class WindowDipToScreenRectPolicyTests
{
    [Fact]
    public void ResolveFromDip_ShouldReturnExpectedPixels_WhenScaleIsOne()
    {
        var rect = SceneResolversPolicies.ResolveFromDip(
            leftDip: 100,
            topDip: 60,
            widthDip: 200,
            heightDip: 48,
            dpiScaleX: 1.0,
            dpiScaleY: 1.0);

        rect.Left.Should().Be(100);
        rect.Top.Should().Be(60);
        rect.Width.Should().Be(200);
        rect.Height.Should().Be(48);
    }

    [Fact]
    public void ResolveFromDip_ShouldScaleBounds_WhenScaleIsOnePointFive()
    {
        var rect = SceneResolversPolicies.ResolveFromDip(
            leftDip: 100,
            topDip: 60,
            widthDip: 200,
            heightDip: 48,
            dpiScaleX: 1.5,
            dpiScaleY: 1.5);

        rect.Left.Should().Be(150);
        rect.Top.Should().Be(90);
        rect.Width.Should().Be(300);
        rect.Height.Should().Be(72);
    }

    [Fact]
    public void ResolveFromDip_ShouldFallbackToScaleOne_WhenScaleIsInvalid()
    {
        var rect = SceneResolversPolicies.ResolveFromDip(
            leftDip: 12.4,
            topDip: 8.6,
            widthDip: 0,
            heightDip: 0,
            dpiScaleX: 0,
            dpiScaleY: -1);

        rect.Left.Should().Be(12);
        rect.Top.Should().Be(8);
        rect.Width.Should().Be(1);
        rect.Height.Should().Be(1);
    }
}
