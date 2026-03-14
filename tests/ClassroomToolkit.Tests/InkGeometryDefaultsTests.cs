using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkGeometryDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkGeometryDefaults.MinSelectionRectSideDip.Should().Be(1.0);
        InkGeometryDefaults.MinShapeStrokeThicknessDip.Should().Be(1.0);
        InkGeometryDefaults.MinShapeRectSideDip.Should().Be(1.0);
        InkGeometryDefaults.MinPenThicknessDip.Should().Be(1.0);
        InkGeometryDefaults.MinEraserRadiusDip.Should().Be(2.0);
        InkGeometryDefaults.EraserMoveThresholdMinDip.Should().Be(1.0);
        InkGeometryDefaults.EraserMoveThresholdScale.Should().Be(0.2);
        InkGeometryDefaults.EraserTapDistanceThresholdDip.Should().Be(0.5);
    }
}
