using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolSizePolicyTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizeBrushSize_ShouldPreserveCurrentFiniteSize_WhenRequestIsNonFinite(double requestedSize)
    {
        PaintToolSizePolicy.NormalizeBrushSize(requestedSize, currentSize: 18.0)
            .Should()
            .Be(18.0);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizeEraserSize_ShouldPreserveCurrentFiniteSize_WhenRequestIsNonFinite(double requestedSize)
    {
        PaintToolSizePolicy.NormalizeEraserSize(requestedSize, currentSize: 30.0)
            .Should()
            .Be(30.0);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(-10.0, 1.0)]
    [InlineData(14.5, 14.5)]
    public void NormalizeBrushSize_ShouldPreserveFiniteExistingBehavior(double requestedSize, double expectedSize)
    {
        PaintToolSizePolicy.NormalizeBrushSize(requestedSize, currentSize: 12.0)
            .Should()
            .Be(expectedSize);
    }

    [Theory]
    [InlineData(0.0, 4.0)]
    [InlineData(-10.0, 4.0)]
    [InlineData(30.5, 30.5)]
    public void NormalizeEraserSize_ShouldPreserveFiniteExistingBehavior(double requestedSize, double expectedSize)
    {
        PaintToolSizePolicy.NormalizeEraserSize(requestedSize, currentSize: 24.0)
            .Should()
            .Be(expectedSize);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizeBrushSize_ShouldUseDefault_WhenRequestAndCurrentAreNonFinite(double requestedSize)
    {
        PaintToolSizePolicy.NormalizeBrushSize(requestedSize, double.NaN)
            .Should()
            .Be(PaintToolSizePolicy.BrushSizeDefault);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizeEraserSize_ShouldUseDefault_WhenRequestAndCurrentAreNonFinite(double requestedSize)
    {
        PaintToolSizePolicy.NormalizeEraserSize(requestedSize, double.NaN)
            .Should()
            .Be(PaintToolSizePolicy.EraserSizeDefault);
    }
}
