using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNavigationCurrentInkRefreshPolicyTests
{
    [Theory]
    [InlineData(true, true, true, (int)PaintToolMode.Brush)]
    [InlineData(true, true, true, (int)PaintToolMode.Eraser)]
    public void ShouldRequest_ShouldReturnTrue_ForInteractivePageSwitchWithInkMutationTools(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var shouldRequest = CrossPageNavigationCurrentInkRefreshPolicy.ShouldRequest(
            pageChanged,
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        shouldRequest.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, true, (int)PaintToolMode.Brush)]
    [InlineData(true, false, true, (int)PaintToolMode.Eraser)]
    [InlineData(true, false, true, (int)PaintToolMode.RegionErase)]
    public void ShouldRequest_ShouldReturnTrue_ForStablePageSwitchWithInkMutationTools(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var shouldRequest = CrossPageNavigationCurrentInkRefreshPolicy.ShouldRequest(
            pageChanged,
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        shouldRequest.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false, true, (int)PaintToolMode.Brush)]
    [InlineData(false, true, true, (int)PaintToolMode.Brush)]
    [InlineData(true, false, false, (int)PaintToolMode.Brush)]
    [InlineData(true, false, true, (int)PaintToolMode.Cursor)]
    [InlineData(true, false, true, (int)PaintToolMode.Shape)]
    [InlineData(true, true, true, (int)PaintToolMode.RegionErase)]
    public void ShouldRequest_ShouldReturnFalse_WhenContextDoesNotRequireRefresh(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var shouldRequest = CrossPageNavigationCurrentInkRefreshPolicy.ShouldRequest(
            pageChanged,
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        shouldRequest.Should().BeFalse();
    }
}
