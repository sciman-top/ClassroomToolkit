using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolbarEventCallbackSafetyContractTests
{
    [Fact]
    public void ToolbarCallbacks_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("ModeChanged?.Invoke(mode)");
        source.Should().Contain("BrushColorChanged?.Invoke(selectedColor)");
        source.Should().Contain("BoardColorChanged?.Invoke(color)");
        source.Should().Contain("ClearRequested?.Invoke()");
        source.Should().Contain("UndoRequested?.Invoke()");
        source.Should().Contain("PhotoOpenRequested?.Invoke()");
        source.Should().Contain("RegionCaptureRequested?.Invoke()");
        source.Should().Contain("QuickColorSlotChanged?.Invoke(index, color)");
        source.Should().Contain("SettingsRequested?.Invoke()");
        source.Should().Contain("ShapeTypeChanged?.Invoke(_shapeType)");
        source.Should().Contain("WhiteboardToggled?.Invoke(_boardActive)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintToolbarWindow.xaml.cs");
    }
}
