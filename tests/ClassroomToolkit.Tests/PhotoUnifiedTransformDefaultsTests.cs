using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoUnifiedTransformDefaultsTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizeScale_ShouldUseDefaultForNonFiniteValues(double value)
    {
        PhotoUnifiedTransformDefaults.NormalizeScale(value)
            .Should()
            .Be(PhotoTransformViewportDefaults.DefaultScale);
    }

    [Theory]
    [InlineData(0.1, 0.2)]
    [InlineData(5.0, 4.0)]
    [InlineData(1.5, 1.5)]
    public void NormalizeScale_ShouldClampToViewportBounds(double value, double expected)
    {
        PhotoUnifiedTransformDefaults.NormalizeScale(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalizeTranslation_ShouldUseDefaultForNonFiniteValues(double value)
    {
        PhotoUnifiedTransformDefaults.NormalizeTranslation(value)
            .Should()
            .Be(PhotoUnifiedTransformDefaults.DefaultTranslateDip);
    }

    [Fact]
    public void NormalizeTranslation_ShouldPreserveFiniteValues()
    {
        PhotoUnifiedTransformDefaults.NormalizeTranslation(-123.5).Should().Be(-123.5);
    }
}
