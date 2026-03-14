using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StylusMoveExecutionPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true, (int)PaintToolMode.Brush, true, (int)StylusMoveExecutionAction.None, false)]
    [InlineData(false, true, true, true, (int)PaintToolMode.Brush, true, (int)StylusMoveExecutionAction.None, false)]
    [InlineData(false, false, false, true, (int)PaintToolMode.Brush, true, (int)StylusMoveExecutionAction.None, false)]
    [InlineData(false, false, true, false, (int)PaintToolMode.Brush, true, (int)StylusMoveExecutionAction.HandlePointerPosition, true)]
    [InlineData(false, false, true, true, (int)PaintToolMode.Brush, true, (int)StylusMoveExecutionAction.HandleBrushBatch, true)]
    [InlineData(false, false, true, true, (int)PaintToolMode.Eraser, false, (int)StylusMoveExecutionAction.HandleStylusPointsIndividually, true)]
    public void Resolve_ShouldReturnExpectedPlan(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints,
        int mode,
        bool strokeInProgress,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = StylusMoveExecutionPolicy.Resolve(
            photoLoading,
            handledByPhotoPan,
            inkOperationActive,
            hasStylusPoints,
            (PaintToolMode)mode,
            strokeInProgress);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}
