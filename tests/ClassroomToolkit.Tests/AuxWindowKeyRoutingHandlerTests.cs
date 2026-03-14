using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AuxWindowKeyRoutingHandlerTests
{
    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenOverlayNotVisible()
    {
        var forwarded = false;

        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: false,
            tryHandlePhotoKey: _ => true,
            canRoutePresentationInput: true,
            forwardPresentationKey: _ => forwarded = true);

        handled.Should().BeFalse();
        forwarded.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnTrue_WhenPhotoHandlerConsumesKey()
    {
        var forwarded = false;
        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.Right,
            overlayVisible: true,
            tryHandlePhotoKey: key => key == Key.Right,
            canRoutePresentationInput: true,
            forwardPresentationKey: _ => forwarded = true);

        handled.Should().BeTrue();
        forwarded.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldForwardPresentation_WhenPhotoDoesNotConsumeAndKeySupported()
    {
        Key? forwardedKey = null;
        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
            canRoutePresentationInput: true,
            forwardPresentationKey: key => forwardedKey = key);

        handled.Should().BeTrue();
        forwardedKey.Should().Be(Key.PageDown);
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenKeyNotSupported()
    {
        var forwarded = false;
        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.A,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
            canRoutePresentationInput: true,
            forwardPresentationKey: _ => forwarded = true);

        handled.Should().BeFalse();
        forwarded.Should().BeFalse();
    }
}
