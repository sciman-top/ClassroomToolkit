using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoCursorModeFocusPolicyTests
{
    [Fact]
    public void ShouldFocusOverlay_ShouldReturnTrue_WhenPhotoModeActive()
    {
        PhotoCursorModeFocusPolicy.ShouldFocusOverlay(photoModeActive: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldFocusOverlay_ShouldReturnFalse_WhenPhotoModeInactive()
    {
        PhotoCursorModeFocusPolicy.ShouldFocusOverlay(photoModeActive: false).Should().BeFalse();
    }
}
