using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolbarDragModeContractTests
{
    [Fact]
    public void ToolbarDrag_ShouldUseManagedMouseCaptureFlow_InsteadOfDragMove()
    {
        var source = string.Join(
            Environment.NewLine,
            File.ReadAllText(GetSourcePath("PaintToolbarWindow.xaml.cs")),
            File.ReadAllText(GetSourcePath("PaintToolbarWindow.Pointer.cs")));

        source.Should().NotContain("PaintActionInvoker.TryInvoke(DragMove);");
        source.Should().Contain("CaptureMouse();");
        source.Should().Contain("ReleaseMouseCapture();");
        source.Should().Contain("private void OnToolbarDragStart(");
        source.Should().Contain("private void OnToolbarDragMove(");
        source.Should().Contain("private void OnToolbarDragEnd(");
        source.Should().Contain("private void OnToolbarTouchDragStart(");
        source.Should().Contain("private void OnToolbarTouchDragMove(");
        source.Should().Contain("private void OnToolbarTouchDragEnd(");
        source.Should().Contain("private void MoveToolbarWithinVirtualScreen(");
    }

    private static string GetSourcePath(string fileName)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            fileName);
    }
}
