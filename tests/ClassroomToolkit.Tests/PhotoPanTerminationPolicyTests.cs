using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanTerminationPolicyTests
{
    [Fact]
    public void ShouldEndPan_ShouldReturnTrue_WhenNoButtonPressed()
    {
        PhotoPanTerminationPolicy.ShouldEndPan(
                shouldAllowPhotoPan: true,
                MouseButtonState.Released,
                MouseButtonState.Released)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenLeftPressed()
    {
        PhotoPanTerminationPolicy.ShouldEndPan(
                shouldAllowPhotoPan: true,
                MouseButtonState.Pressed,
                MouseButtonState.Released)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenRightPressed()
    {
        PhotoPanTerminationPolicy.ShouldEndPan(
                shouldAllowPhotoPan: true,
                MouseButtonState.Released,
                MouseButtonState.Pressed)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnTrue_WhenPanNoLongerAllowed()
    {
        PhotoPanTerminationPolicy.ShouldEndPan(
                shouldAllowPhotoPan: false,
                MouseButtonState.Pressed,
                MouseButtonState.Pressed)
            .Should()
            .BeTrue();
    }
}
