using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarResumeCancellationPolicyTests
{
    [Fact]
    public void ShouldCancelPendingResumeOnToolbarPress_ShouldReturnTrue_ForNonBoardButtonWhileResumeIsArmed()
    {
        var shouldCancel = ToolbarResumeCancellationPolicy.ShouldCancelPendingResumeOnToolbarPress(
            resumeArmed: true,
            pressedToolbarButton: true,
            pressedBoardButton: false);

        shouldCancel.Should().BeTrue();
    }

    [Fact]
    public void ShouldCancelPendingResumeOnToolbarPress_ShouldReturnFalse_ForBoardButton()
    {
        var shouldCancel = ToolbarResumeCancellationPolicy.ShouldCancelPendingResumeOnToolbarPress(
            resumeArmed: true,
            pressedToolbarButton: true,
            pressedBoardButton: true);

        shouldCancel.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ShouldCancelPendingResumeOnToolbarPress_ShouldReturnFalse_WhenNoPendingResumeOrNoButton(
        bool resumeArmed,
        bool pressedToolbarButton,
        bool pressedBoardButton)
    {
        var shouldCancel = ToolbarResumeCancellationPolicy.ShouldCancelPendingResumeOnToolbarPress(
            resumeArmed,
            pressedToolbarButton,
            pressedBoardButton);

        shouldCancel.Should().BeFalse();
    }
}
