using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class SceneCardsLayoutPolicyTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        SceneCardsLayoutPolicy.SingleColumnThreshold.Should().Be(860);
    }

    [Theory]
    [InlineData(600, 1)]
    [InlineData(859.99, 1)]
    [InlineData(860, 0)]
    [InlineData(1200, 0)]
    public void Resolve_ShouldReturnExpectedMode_ForFiniteWidths(double width, int expectedMode)
    {
        SceneCardsLayoutPolicy.Resolve(width).Should().Be((SceneCardsLayoutMode)expectedMode);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Resolve_ShouldFallbackToTwoColumns_ForInvalidWidths(double width)
    {
        SceneCardsLayoutPolicy.Resolve(width).Should().Be(SceneCardsLayoutMode.TwoColumns);
    }
}
