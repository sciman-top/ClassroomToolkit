using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AuxWindowKeyRoutingHandlerTests
{
    [Fact]
    public void TryHandle_ShouldThrowArgumentNullException_WhenPhotoHandlerIsNull()
    {
        var act = () => AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: true,
            tryHandlePhotoKey: null!,
            canRoutePresentationInput: true,
            tryForwardPresentationKey: _ => true);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryHandle_ShouldThrowArgumentNullException_WhenForwardHandlerIsNull()
    {
        var act = () => AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
            canRoutePresentationInput: true,
            tryForwardPresentationKey: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenOverlayNotVisible()
    {
        var forwarded = false;

        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: false,
            tryHandlePhotoKey: _ => true,
            canRoutePresentationInput: true,
            tryForwardPresentationKey: _ =>
            {
                forwarded = true;
                return true;
            });

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
            tryForwardPresentationKey: _ =>
            {
                forwarded = true;
                return true;
            });

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
            tryForwardPresentationKey: key =>
            {
                forwardedKey = key;
                return true;
            });

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
            tryForwardPresentationKey: _ =>
            {
                forwarded = true;
                return true;
            });

        handled.Should().BeFalse();
        forwarded.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenPhotoHandlerThrowsNonFatal()
    {
        var forwarded = false;

        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.Right,
            overlayVisible: true,
            tryHandlePhotoKey: _ => throw new InvalidOperationException("photo-failed"),
            canRoutePresentationInput: false,
            tryForwardPresentationKey: _ =>
            {
                forwarded = true;
                return true;
            });

        handled.Should().BeFalse();
        forwarded.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenForwardHandlerThrowsNonFatal()
    {
        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
            canRoutePresentationInput: true,
            tryForwardPresentationKey: _ => throw new InvalidOperationException("forward-failed"));

        handled.Should().BeFalse();
    }

    [Fact]
    public void TryHandle_ShouldReturnFalse_WhenForwardReturnsFalse()
    {
        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key: Key.PageDown,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
            canRoutePresentationInput: true,
            tryForwardPresentationKey: _ => false);

        handled.Should().BeFalse();
    }
}
