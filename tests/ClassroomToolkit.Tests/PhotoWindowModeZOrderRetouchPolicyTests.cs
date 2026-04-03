using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoWindowModeZOrderRetouchPolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldRequest_ShouldRequireActivePhotoModeAndFullscreenStateChange(
        bool photoModeActive,
        bool fullscreenChanged,
        bool expected)
    {
        PhotoWindowModeZOrderRetouchPolicy.ShouldRequest(photoModeActive, fullscreenChanged).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldForceEnforce_ShouldFollowCurrentFullscreen(bool fullscreen, bool expected)
    {
        PhotoWindowModeZOrderRetouchPolicy.ShouldForceEnforce(fullscreen).Should().Be(expected);
    }
}
