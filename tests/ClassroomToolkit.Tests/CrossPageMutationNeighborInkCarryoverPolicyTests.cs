using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageMutationNeighborInkCarryoverPolicyTests
{
    [Fact]
    public void ShouldClearPreservedNeighborInkFrames_ShouldReturnTrue_ForBrushMutationSwitch()
    {
        var shouldClear = CrossPageMutationNeighborInkCarryoverPolicy.ShouldClearPreservedNeighborInkFrames(
            pageChanged: true,
            interactiveSwitch: false,
            inputTriggeredByActiveInkMutation: true,
            mode: PaintToolMode.Brush);

        shouldClear.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false, true, PaintToolMode.Brush)]
    [InlineData(true, true, true, PaintToolMode.Brush)]
    [InlineData(true, false, false, PaintToolMode.Brush)]
    [InlineData(true, false, true, PaintToolMode.Eraser)]
    [InlineData(true, false, true, PaintToolMode.RegionErase)]
    public void ShouldClearPreservedNeighborInkFrames_ShouldReturnFalse_WhenGuardNotMet(
        bool pageChanged,
        bool interactiveSwitch,
        bool inputTriggeredByActiveInkMutation,
        PaintToolMode mode)
    {
        var shouldClear = CrossPageMutationNeighborInkCarryoverPolicy.ShouldClearPreservedNeighborInkFrames(
            pageChanged,
            interactiveSwitch,
            inputTriggeredByActiveInkMutation,
            mode);

        shouldClear.Should().BeFalse();
    }
}
