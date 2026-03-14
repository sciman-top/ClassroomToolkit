using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPresentationRoutingPolicyTests
{
    [Fact]
    public void CanRouteFromAuxWindow_ShouldAllow_WhenPresentationInputAllowed_AndNotPhotoBoard()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromAuxWindow(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromAuxWindow_ShouldDeny_WhenNavigationDisabled()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromAuxWindow(
            UiNavigationMode.Disabled,
            photoModeActive: false,
            boardActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromAuxWindow_ShouldDeny_WhenPhotoOrBoardActive()
    {
        var photo = OverlayPresentationRoutingPolicy.CanRouteFromAuxWindow(
            UiNavigationMode.Hybrid,
            photoModeActive: true,
            boardActive: false);
        var board = OverlayPresentationRoutingPolicy.CanRouteFromAuxWindow(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: true);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenHybridAndNotPhotoBoard()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenHookOnlyAndNotPhotoBoard()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.HookOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenDrawModeAndHookOnly()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.HookOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenNavigationDisabled()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.Disabled,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inputPassthroughEnabled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenDrawModeButNotHookOrHybrid()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.MessageOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inputPassthroughEnabled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenPhotoOrBoardActive()
    {
        var photo = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);
        var board = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: true,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenCursorPassthroughEnabled()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenCursorAndHookOnly()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.HookOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenCursorAndMessageOnly()
    {
        var result = OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            UiNavigationMode.MessageOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeFalse();
    }
}
