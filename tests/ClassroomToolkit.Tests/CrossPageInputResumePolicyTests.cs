using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputResumePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenNotSwitched()
    {
        var plan = CrossPageInputResumePolicy.Resolve(
            switchedPage: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: false,
            isErasing: false,
            replayCurrentInput: false,
            hasPendingBrushSeed: false,
            pendingSeedEqualsInput: true);

        plan.Action.Should().Be(CrossPageInputResumeAction.None);
        plan.ShouldClearPendingBrushState.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    public void Resolve_ShouldReturnBrushContinuationPlan_WhenBrushAndNoStrokeInProgress(
        bool replayCurrentInput,
        bool hasPendingBrushSeed,
        bool pendingSeedEqualsInput,
        bool expectedShouldUpdate)
    {
        var plan = CrossPageInputResumePolicy.Resolve(
            switchedPage: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: false,
            isErasing: false,
            replayCurrentInput: replayCurrentInput,
            hasPendingBrushSeed: hasPendingBrushSeed,
            pendingSeedEqualsInput: pendingSeedEqualsInput);

        plan.Action.Should().Be(CrossPageInputResumeAction.BeginBrushContinuation);
        plan.ShouldClearPendingBrushState.Should().BeTrue();
        plan.ShouldUpdateBrushAfterContinuation.Should().Be(expectedShouldUpdate);
    }

    [Fact]
    public void Resolve_ShouldReturnBeginEraser_WhenEraserAndNotErasing()
    {
        var plan = CrossPageInputResumePolicy.Resolve(
            switchedPage: true,
            mode: PaintToolMode.Eraser,
            strokeInProgress: false,
            isErasing: false,
            replayCurrentInput: false,
            hasPendingBrushSeed: false,
            pendingSeedEqualsInput: true);

        plan.Action.Should().Be(CrossPageInputResumeAction.BeginEraser);
        plan.ShouldClearPendingBrushState.Should().BeFalse();
    }
}
