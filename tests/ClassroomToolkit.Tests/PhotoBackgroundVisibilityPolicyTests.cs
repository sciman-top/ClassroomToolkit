using ClassroomToolkit.App.Paint;
using FluentAssertions;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoBackgroundVisibilityPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnVisible_WhenPhotoModeOn_BoardOff_AndSourceExists()
    {
        var visibility = PhotoBackgroundVisibilityPolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            hasBackgroundSource: true);

        visibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Resolve_ShouldReturnCollapsed_WhenBoardIsActive()
    {
        var visibility = PhotoBackgroundVisibilityPolicy.Resolve(
            photoModeActive: true,
            boardActive: true,
            hasBackgroundSource: true);

        visibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Resolve_ShouldReturnCollapsed_WhenSourceMissing()
    {
        var visibility = PhotoBackgroundVisibilityPolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            hasBackgroundSource: false);

        visibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Resolve_ShouldReturnCollapsed_WhenPhotoModeDisabled()
    {
        var visibility = PhotoBackgroundVisibilityPolicy.Resolve(
            photoModeActive: false,
            boardActive: false,
            hasBackgroundSource: true);

        visibility.Should().Be(Visibility.Collapsed);
    }
}
