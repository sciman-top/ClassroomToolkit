using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoWindowStateRestorePolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldArmFullscreenRestore_ShouldFollowCurrentPhotoFullscreen(bool photoFullscreen, bool expected)
    {
        PhotoWindowStateRestorePolicy.ShouldArmFullscreenRestore(photoFullscreen).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, WindowState.Normal, true)]
    [InlineData(true, WindowState.Maximized, true)]
    [InlineData(true, WindowState.Minimized, false)]
    [InlineData(false, WindowState.Normal, false)]
    [InlineData(false, WindowState.Maximized, false)]
    [InlineData(false, WindowState.Minimized, false)]
    public void ShouldRestoreFullscreen_ShouldRequirePendingFlagAndNonMinimizedWindow(
        bool pendingRestore,
        WindowState windowState,
        bool expected)
    {
        PhotoWindowStateRestorePolicy.ShouldRestoreFullscreen(pendingRestore, windowState).Should().Be(expected);
    }
}
