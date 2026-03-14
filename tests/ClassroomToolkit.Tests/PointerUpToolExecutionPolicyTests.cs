using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PointerUpToolExecutionPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, true, (int)PointerUpToolAction.EndBrushStroke, true)]
    [InlineData((int)PaintToolMode.Brush, false, (int)PointerUpToolAction.EndBrushStroke, false)]
    [InlineData((int)PaintToolMode.Eraser, true, (int)PointerUpToolAction.EndEraser, false)]
    [InlineData((int)PaintToolMode.RegionErase, false, (int)PointerUpToolAction.EndRegionSelection, false)]
    [InlineData((int)PaintToolMode.Shape, false, (int)PointerUpToolAction.EndShape, false)]
    [InlineData((int)PaintToolMode.Cursor, true, (int)PointerUpToolAction.None, false)]
    public void Resolve_ShouldReturnExpectedPlan(
        int mode,
        bool pendingAdaptiveRendererRefresh,
        int expectedAction,
        bool expectedRefresh)
    {
        var plan = PointerUpToolExecutionPolicy.Resolve(
            (PaintToolMode)mode,
            pendingAdaptiveRendererRefresh);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldRefreshAdaptiveRenderer.Should().Be(expectedRefresh);
    }
}
