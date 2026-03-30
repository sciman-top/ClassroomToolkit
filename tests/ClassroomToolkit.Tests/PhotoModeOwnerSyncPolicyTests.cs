using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoModeOwnerSyncPolicyTests
{
    [Fact]
    public void ShouldSyncOwners_ShouldReturnTrue_WhenNotTouchingPhotoFullscreenSurface()
    {
        PhotoModeOwnerSyncPolicy.ShouldSyncOwners(touchPhotoFullscreenSurface: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldSyncOwners_ShouldReturnFalse_WhenTouchingPhotoFullscreenSurface()
    {
        PhotoModeOwnerSyncPolicy.ShouldSyncOwners(touchPhotoFullscreenSurface: true).Should().BeFalse();
    }
}
