using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoRightClickContextMenuPolicyTests
{
    [Fact]
    public void ShouldArmPending_ShouldReturnTrue_WhenPhotoFullscreenCursor()
    {
        PhotoRightClickContextMenuPolicy.ShouldArmPending(
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldArmPending_ShouldReturnFalse_WhenModeOrStateMismatch()
    {
        PhotoRightClickContextMenuPolicy.ShouldArmPending(
                photoModeActive: false,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeFalse();

        PhotoRightClickContextMenuPolicy.ShouldArmPending(
                photoModeActive: true,
                photoFullscreen: false,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeFalse();

        PhotoRightClickContextMenuPolicy.ShouldArmPending(
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Brush)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldCancelPendingByMove_ShouldUseThresholdDip()
    {
        var threshold = PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip;
        PhotoRightClickContextMenuPolicy.ShouldCancelPendingByMove(new Vector(threshold - 1, 0))
            .Should()
            .BeFalse();

        PhotoRightClickContextMenuPolicy.ShouldCancelPendingByMove(new Vector(threshold + 1, 0))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldShowContextMenuOnUp_ShouldRequirePendingAndArmState()
    {
        PhotoRightClickContextMenuPolicy.ShouldShowContextMenuOnUp(
                rightClickPending: true,
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeTrue();

        PhotoRightClickContextMenuPolicy.ShouldShowContextMenuOnUp(
                rightClickPending: false,
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeFalse();
    }
}
