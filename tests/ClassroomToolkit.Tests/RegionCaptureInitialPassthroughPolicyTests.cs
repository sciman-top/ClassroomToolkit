using System.Drawing;
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
}
