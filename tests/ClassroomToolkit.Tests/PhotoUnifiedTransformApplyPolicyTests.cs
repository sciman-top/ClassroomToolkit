using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoUnifiedTransformApplyPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldApplyRuntimeTransform_ShouldMatchExpected(
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool expected)
    {
        PhotoUnifiedTransformApplyPolicy.ShouldApplyRuntimeTransform(
                photoInkModeActive,
                crossPageDisplayActive)
            .Should()
            .Be(expected);
    }
}
