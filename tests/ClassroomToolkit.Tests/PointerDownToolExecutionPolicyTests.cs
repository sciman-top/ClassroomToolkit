using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PointerDownToolExecutionPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, (int)PointerDownToolAction.BeginBrushStroke, true)]
    [InlineData((int)PaintToolMode.Eraser, (int)PointerDownToolAction.BeginEraser, true)]
    [InlineData((int)PaintToolMode.RegionErase, (int)PointerDownToolAction.BeginRegionSelection, true)]
    [InlineData((int)PaintToolMode.Shape, (int)PointerDownToolAction.BeginShape, true)]
    [InlineData((int)PaintToolMode.Cursor, (int)PointerDownToolAction.None, false)]
    public void Resolve_ShouldReturnExpectedPlan(int mode, int expectedAction, bool shouldCapturePointer)
    {
        var plan = PointerDownToolExecutionPolicy.Resolve((PaintToolMode)mode);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldCapturePointer.Should().Be(shouldCapturePointer);
    }
}
