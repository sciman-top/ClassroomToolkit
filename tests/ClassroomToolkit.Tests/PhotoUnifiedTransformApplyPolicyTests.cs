using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoUnifiedTransformApplyPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ShouldApplyRuntimeTransform_ShouldMatchExpected(
        bool rememberPhotoTransform,
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool expected)
    {
        PhotoUnifiedTransformApplyPolicy.ShouldApplyRuntimeTransform(
                rememberPhotoTransform,
                photoInkModeActive,
                crossPageDisplayActive)
            .Should()
            .Be(expected);
    }
}
