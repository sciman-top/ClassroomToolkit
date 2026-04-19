using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageZoomLayoutScalePolicyTests
{
    [Theory]
    [InlineData(1.25)]
    [InlineData(0.8)]
    public void ShouldSynchronize_ShouldReturnTrue_ForMeaningfulScaleFactors(double scaleFactor)
    {
        CrossPageZoomLayoutScalePolicy.ShouldSynchronize(scaleFactor).Should().BeTrue();
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.0005)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void ShouldSynchronize_ShouldReturnFalse_ForInvalidOrTinyScaleFactors(double scaleFactor)
    {
        CrossPageZoomLayoutScalePolicy.ShouldSynchronize(scaleFactor).Should().BeFalse();
    }

    [Fact]
    public void Scale_ShouldMultiplyPositiveHeightsAndNegativeOffsets()
    {
        CrossPageZoomLayoutScalePolicy.Scale(240.0, 0.5).Should().Be(120.0);
        CrossPageZoomLayoutScalePolicy.Scale(-180.0, 0.5).Should().Be(-90.0);
    }
}
