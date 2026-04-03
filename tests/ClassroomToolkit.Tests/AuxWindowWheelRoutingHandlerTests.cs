using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AuxWindowWheelRoutingHandlerTests
{
    [Fact]
    public void TryHandle_ShouldThrow_WhenForwarderIsNull()
    {
        var act = () => AuxWindowWheelRoutingHandler.TryHandle(
            delta: 120,
            overlayVisible: true,
            canRoutePresentationInput: true,
            tryForwardPresentationWheel: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenOverlayNotVisible()
    {
        var handled = AuxWindowWheelRoutingHandler.TryHandle(
            delta: 120,
            overlayVisible: false,
            canRoutePresentationInput: true,
            tryForwardPresentationWheel: _ => true);

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenCannotRoute()
    {
        var handled = AuxWindowWheelRoutingHandler.TryHandle(
            delta: 120,
            overlayVisible: true,
            canRoutePresentationInput: false,
            tryForwardPresentationWheel: _ => true);

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenDeltaZero()
    {
        var handled = AuxWindowWheelRoutingHandler.TryHandle(
            delta: 0,
            overlayVisible: true,
            canRoutePresentationInput: true,
            tryForwardPresentationWheel: _ => true);

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnForwardResult()
    {
        var handled = AuxWindowWheelRoutingHandler.TryHandle(
            delta: -120,
            overlayVisible: true,
            canRoutePresentationInput: true,
            tryForwardPresentationWheel: delta => delta < 0);

        handled.Should().BeTrue();
    }
}
