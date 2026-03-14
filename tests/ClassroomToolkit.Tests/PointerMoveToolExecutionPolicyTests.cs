using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PointerMoveToolExecutionPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, (int)PointerMoveToolAction.UpdateBrushStroke)]
    [InlineData((int)PaintToolMode.Eraser, (int)PointerMoveToolAction.UpdateEraser)]
    [InlineData((int)PaintToolMode.RegionErase, (int)PointerMoveToolAction.UpdateRegionSelection)]
    [InlineData((int)PaintToolMode.Shape, (int)PointerMoveToolAction.UpdateShapePreview)]
    [InlineData((int)PaintToolMode.Cursor, (int)PointerMoveToolAction.None)]
    public void Resolve_ShouldReturnExpectedAction(int mode, int expectedAction)
    {
        var action = PointerMoveToolExecutionPolicy.Resolve((PaintToolMode)mode);

        ((int)action).Should().Be(expectedAction);
    }
}
