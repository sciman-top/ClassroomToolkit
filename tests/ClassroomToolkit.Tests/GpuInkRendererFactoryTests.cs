using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class GpuInkRendererFactoryTests
{
    [Fact]
    public void BackendId_ShouldBeGpu()
    {
        var factory = new GpuInkRendererFactory();

        factory.BackendId.Should().Be("gpu");
    }

    [Fact]
    public void Create_ShouldFallbackToCompatibleRenderer()
    {
        var factory = new GpuInkRendererFactory();

        var renderer = factory.Create(
            PaintBrushStyle.StandardRibbon,
            MarkerBrushConfig.Balanced,
            BrushPhysicsConfig.CreateCalligraphyBalanced());

        var marker = renderer.Should().BeOfType<MarkerBrushRenderer>().Subject;
        marker.RenderMode.Should().Be(MarkerRenderMode.Ribbon);
    }
}
