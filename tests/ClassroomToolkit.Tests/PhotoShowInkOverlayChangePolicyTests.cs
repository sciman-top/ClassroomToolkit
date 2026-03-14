using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoShowInkOverlayChangePolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void ShouldApply_ShouldMatchExpected(bool currentEnabled, bool nextEnabled, bool expected)
    {
        PhotoShowInkOverlayChangePolicy.ShouldApply(currentEnabled, nextEnabled)
            .Should()
            .Be(expected);
    }
}
