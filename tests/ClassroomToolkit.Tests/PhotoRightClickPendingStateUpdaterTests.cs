using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoRightClickPendingStateUpdaterTests
{
    [Fact]
    public void Arm_ShouldSetPendingAndStartPoint()
    {
        var pending = false;
        var start = new Point(0, 0);

        PhotoRightClickPendingStateUpdater.Arm(
            ref pending,
            ref start,
            new Point(10, 20));

        pending.Should().BeTrue();
        start.Should().Be(new Point(10, 20));
    }

    [Fact]
    public void Clear_ShouldResetPending()
    {
        var pending = true;

        PhotoRightClickPendingStateUpdater.Clear(ref pending);

        pending.Should().BeFalse();
    }

    [Fact]
    public void UpdateByMove_ShouldCancel_WhenMoveExceedsThreshold()
    {
        var pending = true;
        var start = new Point(0, 0);
        var current = new Point(PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip + 5, 0);

        PhotoRightClickPendingStateUpdater.UpdateByMove(ref pending, start, current);

        pending.Should().BeFalse();
    }
}
