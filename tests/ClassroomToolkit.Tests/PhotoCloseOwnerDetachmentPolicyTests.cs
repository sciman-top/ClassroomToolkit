using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoCloseOwnerDetachmentPolicyTests
{
    [Fact]
    public void ShouldDetachOwners_ShouldReturnTrue_WhenSyncOwnersNotVisible()
    {
        PhotoCloseOwnerDetachmentPolicy.ShouldDetachOwners(syncFloatingOwnersVisible: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldDetachOwners_ShouldReturnFalse_WhenSyncOwnersVisible()
    {
        PhotoCloseOwnerDetachmentPolicy.ShouldDetachOwners(syncFloatingOwnersVisible: true).Should().BeFalse();
    }
}
