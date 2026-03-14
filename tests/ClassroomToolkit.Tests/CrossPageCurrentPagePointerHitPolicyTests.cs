using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageCurrentPagePointerHitPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ShouldUseCurrentPage_ShouldMatchExpected(
        bool hasCurrentBitmap,
        bool hasCurrentRect,
        bool pointerInsideCurrentRect,
        bool expected)
    {
        var result = CrossPageCurrentPagePointerHitPolicy.ShouldUseCurrentPage(
            hasCurrentBitmap,
            hasCurrentRect,
            pointerInsideCurrentRect);

        result.Should().Be(expected);
    }
}
