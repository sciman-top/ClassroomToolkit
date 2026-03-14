using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class OverlayFocusAcceptancePolicyTests
{
    [Fact]
    public void ShouldBlockFocus_ShouldReturnTrue_WhenCursorPassthroughEnabled()
    {
        var result = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.Disabled,
            inputPassthroughEnabled: true,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: false,
            presentationTargetValid: false,
            wpsRawTargetValid: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenPhotoOrBoardActive()
    {
        var photo = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: true,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: false);

        var board = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: true,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: false);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenNavigationModeNotHybridOrHook()
    {
        var result = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.MessageOnly,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnTrue_WhenHybridAndPresentationTargetAvailable()
    {
        var result = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: false,
            wpsRawTargetValid: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenModeIsDraw()
    {
        var result = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Brush,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenPresentationNotAllowed()
    {
        var result = OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: false,
            presentationTargetValid: true,
            wpsRawTargetValid: true);

        result.Should().BeFalse();
    }
}
