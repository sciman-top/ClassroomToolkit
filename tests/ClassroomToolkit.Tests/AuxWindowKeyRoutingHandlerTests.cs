using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Windows.Input;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class AuxWindowKeyRoutingHandlerTests
{
    [Theory]
    [InlineData(Key.PageDown)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.Space)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    public void TryHandle_ShouldForwardMappedPresentationKey(Key key)
    {
        var forwarded = false;

        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            key,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
            canRoutePresentationInput: true,
            tryForwardPresentationKey: forwardedKey =>
            {
                forwarded = forwardedKey == key;
                return forwarded;
            });

        handled.Should().BeTrue();
        forwarded.Should().BeTrue();
    }

    [Fact]
    public void TryHandle_ShouldNotForwardWhenPresentationRoutingIsDisabled()
    {
        var forwarded = false;

        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            Key.PageDown,
            overlayVisible: true,
            tryHandlePhotoKey: _ => false,
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
    public void TryHandle_ShouldNotForwardUnsupportedKey()
    {
        var forwarded = false;

        var handled = AuxWindowKeyRoutingHandler.TryHandle(
            Key.A,
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
}
