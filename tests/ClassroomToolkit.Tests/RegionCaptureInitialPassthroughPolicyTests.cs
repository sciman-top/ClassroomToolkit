using System.Drawing;
using System.IO;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RegionCaptureInitialPassthroughPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPointerMovePassthrough_WhenPointerStartsInsideToolbarRegion()
    {
        var decision = RegionCaptureInitialPassthroughPolicy.Resolve(
            pointerScreenX: 120,
            pointerScreenY: 80,
            passthroughRegions: new[] { new Rectangle(100, 60, 200, 48) });

        decision.ShouldCancel.Should().BeTrue();
        decision.InputKind.Should().Be(RegionScreenCapturePassthroughInputKind.PointerMove);
    }

    [Fact]
    public void Resolve_ShouldReturnNoPassthrough_WhenPointerStartsOutsideToolbarRegion()
    {
        var decision = RegionCaptureInitialPassthroughPolicy.Resolve(
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
