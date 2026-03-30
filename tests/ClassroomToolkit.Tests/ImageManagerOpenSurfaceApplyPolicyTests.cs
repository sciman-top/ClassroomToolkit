using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerOpenSurfaceApplyPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldApply_ShouldMatchExpected(bool touchImageManagerSurface, bool requestZOrderApply, bool expected)
    {
        ImageManagerOpenSurfaceApplyPolicy.ShouldApply(touchImageManagerSurface, requestZOrderApply)
            .Should()
            .Be(expected);
    }
}
