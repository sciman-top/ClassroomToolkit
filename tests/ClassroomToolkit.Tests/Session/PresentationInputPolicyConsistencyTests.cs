using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class PresentationInputPolicyConsistencyTests
{
    [Theory]
    [InlineData(UiNavigationMode.Disabled)]
    [InlineData(UiNavigationMode.MessageOnly)]
    [InlineData(UiNavigationMode.HookOnly)]
    [InlineData(UiNavigationMode.Hybrid)]
    public void RoutingPolicy_ShouldMatchSessionPresentationInputPolicy(UiNavigationMode navigationMode)
    {
        var expected = UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode);
        var actual = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            navigationMode,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(UiNavigationMode.Disabled)]
    [InlineData(UiNavigationMode.MessageOnly)]
    [InlineData(UiNavigationMode.HookOnly)]
    [InlineData(UiNavigationMode.Hybrid)]
    public void FocusAcceptancePolicy_ShouldMatchSessionPresentationInputPolicy_WhenCursorGateIsOpen(
        UiNavigationMode navigationMode)
    {
        var expected = UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode);
        var actual = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            navigationMode,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: false);

        actual.Should().Be(expected);
    }
}
