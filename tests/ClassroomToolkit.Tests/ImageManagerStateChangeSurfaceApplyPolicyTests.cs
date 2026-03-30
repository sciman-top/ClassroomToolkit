using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerStateChangeSurfaceApplyPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldApply_ShouldMatchExpected(bool requestZOrderApply, bool forceEnforceZOrder, bool expected)
    {
        ImageManagerStateChangeSurfaceApplyPolicy.ShouldApply(requestZOrderApply, forceEnforceZOrder)
            .Should()
            .Be(expected);
    }
}
