using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CpuInkRendererFactoryTests
{
    [Fact]
    public void Create_ShouldReturnCalligraphyRenderer_ForCalligraphyStyle()
    {
        var factory = new CpuInkRendererFactory();

        var renderer = factory.Create(
            PaintBrushStyle.Calligraphy,
            MarkerBrushConfig.Balanced,
            BrushPhysicsConfig.CreateCalligraphyBalanced());

        renderer.Should().BeOfType<VariableWidthBrushRenderer>();
    }

    [Fact]
    public void Create_ShouldReturnRibbonMarkerRenderer_ForStandardRibbonStyle()
    {
        var factory = new CpuInkRendererFactory();

        var renderer = factory.Create(
            PaintBrushStyle.StandardRibbon,
            MarkerBrushConfig.Balanced,
            BrushPhysicsConfig.CreateCalligraphyBalanced());

        var marker = renderer.Should().BeOfType<MarkerBrushRenderer>().Subject;
        marker.RenderMode.Should().Be(MarkerRenderMode.Ribbon);
    }

    [Fact]
    public void CanReuse_ShouldRejectMismatchedRendererType()
    {
        var factory = new CpuInkRendererFactory();
        IBrushRenderer renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());

        var canReuse = factory.CanReuse(PaintBrushStyle.StandardRibbon, renderer);

        canReuse.Should().BeFalse();
    }
}
