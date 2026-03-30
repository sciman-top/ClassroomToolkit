using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanMouseMoveRoutingPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPassThrough_WhenNotActive()
    {
        var decision = PhotoPanMouseMoveRoutingPolicy.Resolve(
            isMousePhotoPanActive: false,
            shouldAllowPhotoPan: true,
            leftButton: MouseButtonState.Pressed,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.PassThrough);
    }

    [Fact]
    public void Resolve_ShouldReturnUpdatePan_WhenActiveAndButtonPressed()
    {
        var decision = PhotoPanMouseMoveRoutingPolicy.Resolve(
            isMousePhotoPanActive: true,
            shouldAllowPhotoPan: true,
            leftButton: MouseButtonState.Pressed,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.UpdatePan);
    }

    [Fact]
    public void Resolve_ShouldReturnEndPan_WhenActiveAndNoButtonPressed()
    {
        var decision = PhotoPanMouseMoveRoutingPolicy.Resolve(
            isMousePhotoPanActive: true,
            shouldAllowPhotoPan: true,
            leftButton: MouseButtonState.Released,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.EndPan);
    }

    [Fact]
    public void Resolve_ShouldReturnEndPan_WhenActiveButPanNoLongerAllowed()
    {
        var decision = PhotoPanMouseMoveRoutingPolicy.Resolve(
            isMousePhotoPanActive: true,
            shouldAllowPhotoPan: false,
            leftButton: MouseButtonState.Pressed,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.EndPan);
    }
}
