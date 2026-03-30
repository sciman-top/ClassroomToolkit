using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class AuxWindowNavigationRoutingPolicyTests
{
    [Fact]
    public void ShouldForwardPresentation_ShouldReturnFalse_WhenRoutingDisabled()
    {
        var shouldForward = AuxWindowNavigationRoutingPolicy.ShouldForwardPresentation(
            canRoutePresentationInput: false,
            key: Key.PageDown);

        shouldForward.Should().BeFalse();
    }

    [Theory]
    [InlineData(Key.PageDown)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.Space)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    public void ShouldForwardPresentation_ShouldReturnTrue_ForPresentationNavigationKeys(Key key)
    {
        var shouldForward = AuxWindowNavigationRoutingPolicy.ShouldForwardPresentation(
            canRoutePresentationInput: true,
            key: key);

        shouldForward.Should().BeTrue();
    }

    [Fact]
    public void ShouldForwardPresentation_ShouldReturnFalse_ForUnsupportedKey()
    {
        var shouldForward = AuxWindowNavigationRoutingPolicy.ShouldForwardPresentation(
            canRoutePresentationInput: true,
            key: Key.A);

        shouldForward.Should().BeFalse();
    }
}
