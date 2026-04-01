using FluentAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkRenderSurfacePolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepOriginalSurface_WhenStrokeBoundsStayInsidePageWidth()
    {
        var plan = CrossPageNeighborInkRenderSurfacePolicy.Resolve(
            pagePixelWidth: 1200,
            pagePixelHeight: 1800,
            dpiX: 96,
            pageWidthDip: 1200,
            minStrokeXDip: 10,
            maxStrokeXDip: 1180);

        plan.PixelWidth.Should().Be(1200);
        plan.PixelHeight.Should().Be(1800);
        plan.HorizontalOffsetDip.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldExpandSurfaceAndOffset_WhenStrokeOverflowsBothSides()
    {
        var plan = CrossPageNeighborInkRenderSurfacePolicy.Resolve(
            pagePixelWidth: 1200,
            pagePixelHeight: 1800,
            dpiX: 96,
            pageWidthDip: 1200,
            minStrokeXDip: -32,
            maxStrokeXDip: 1248);

        plan.PixelWidth.Should().Be(1280);
        plan.PixelHeight.Should().Be(1800);
        plan.HorizontalOffsetDip.Should().Be(32);
    }

    [Fact]
    public void Resolve_ShouldClampOverflowToBudget_WhenStrokeExtendsTooFarOutsidePage()
    {
        var plan = CrossPageNeighborInkRenderSurfacePolicy.Resolve(
            pagePixelWidth: 1000,
            pagePixelHeight: 1600,
            dpiX: 96,
            pageWidthDip: 1000,
            minStrokeXDip: -2000,
            maxStrokeXDip: 3200);

        plan.PixelWidth.Should().Be(1000 + (CrossPageNeighborInkRenderSurfacePolicy.MaxHorizontalOverflowDipPerSidePx * 2));
        plan.HorizontalOffsetDip.Should().Be(CrossPageNeighborInkRenderSurfacePolicy.MaxHorizontalOverflowDipPerSidePx);
    }
}
