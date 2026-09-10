using System.Windows;
using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFollowMonitorPolicyTests
{
    [Fact]
    public void ShouldFollow_ShouldReturnTrue_WhenPresentationSceneIsActive()
    {
        PresentationFollowMonitorPolicy.ShouldFollow(
            photoModeActive: false,
            boardActive: false,
            overlayVisible: true,
            windowStateMinimized: false).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, true)]
    public void ShouldFollow_ShouldReturnFalse_WhenSceneOwnsGeometryOrOverlayUnavailable(
        bool photoModeActive,
        bool boardActive,
        bool overlayVisible,
        bool windowStateMinimized)
    {
        PresentationFollowMonitorPolicy.ShouldFollow(
            photoModeActive,
            boardActive,
            overlayVisible,
            windowStateMinimized).Should().BeFalse();
    }

    [Fact]
    public void ShouldMove_ShouldReturnFalse_WhenOverlayAlreadyOnTargetMonitor()
    {
        var rect = new Rect(1920, 0, 1920, 1080);

        PresentationFollowMonitorPolicy.ShouldMove(rect, rect).Should().BeFalse();
    }

    [Fact]
    public void ShouldMove_ShouldReturnTrue_WhenTargetMonitorDiffers()
    {
        PresentationFollowMonitorPolicy.ShouldMove(
            new Rect(0, 0, 1920, 1080),
            new Rect(1920, 0, 1920, 1080)).Should().BeTrue();
    }
}
