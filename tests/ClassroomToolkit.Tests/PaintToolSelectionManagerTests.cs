using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolSelectionManagerTests
{
    [Fact]
    public void Select_ShouldSwitchToRequestedMode_WhenDifferentToolIsSelected()
    {
        var manager = new PaintToolSelectionManager();

        var mode = manager.Select(PaintToolMode.Eraser, allowToggleOffCurrent: true);

        mode.Should().Be(PaintToolMode.Eraser);
        manager.CurrentMode.Should().Be(PaintToolMode.Eraser);
    }

    [Fact]
    public void Select_ShouldFallbackToMostRecentNonCursorMode_WhenCurrentModeIsToggledOff()
    {
        var manager = new PaintToolSelectionManager(PaintToolMode.Brush);
        manager.Select(PaintToolMode.Eraser, allowToggleOffCurrent: true);
        manager.Select(PaintToolMode.RegionErase, allowToggleOffCurrent: true);

        var mode = manager.Select(PaintToolMode.RegionErase, allowToggleOffCurrent: true);

        mode.Should().Be(PaintToolMode.Eraser);
        manager.CurrentMode.Should().Be(PaintToolMode.Eraser);
    }

    [Fact]
    public void Select_ShouldNotStoreCursorInHistory_WhenRestoringFallbackMode()
    {
        var manager = new PaintToolSelectionManager(PaintToolMode.Brush);
        manager.Select(PaintToolMode.Cursor, allowToggleOffCurrent: false);
        manager.Select(PaintToolMode.Eraser, allowToggleOffCurrent: true);

        var mode = manager.Select(PaintToolMode.Eraser, allowToggleOffCurrent: true);

        mode.Should().Be(PaintToolMode.Brush);
    }

    [Fact]
    public void Select_ShouldKeepCursorMode_WhenCursorIsClickedAgain()
    {
        var manager = new PaintToolSelectionManager(PaintToolMode.Brush);
        manager.Select(PaintToolMode.Cursor, allowToggleOffCurrent: false);

        var mode = manager.Select(PaintToolMode.Cursor, allowToggleOffCurrent: false);

        mode.Should().Be(PaintToolMode.Cursor);
        manager.CurrentMode.Should().Be(PaintToolMode.Cursor);
    }

    [Fact]
    public void Select_ShouldFallbackToBrush_WhenNoHistoryExists()
    {
        var manager = new PaintToolSelectionManager(PaintToolMode.Eraser);

        var mode = manager.Select(PaintToolMode.Eraser, allowToggleOffCurrent: true);

        mode.Should().Be(PaintToolMode.Brush);
    }

    [Fact]
    public void ResetToBrushBaseline_ShouldPreventShapeClickFromFallingBackToBrush()
    {
        var manager = new PaintToolSelectionManager(PaintToolMode.Brush);
        manager.Select(PaintToolMode.Shape, allowToggleOffCurrent: true);
        manager.Reset(PaintToolMode.Brush);

        var mode = manager.Select(PaintToolMode.Shape, allowToggleOffCurrent: true);

        mode.Should().Be(PaintToolMode.Shape);
        manager.CurrentMode.Should().Be(PaintToolMode.Shape);
    }
}
