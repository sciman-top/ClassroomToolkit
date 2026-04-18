using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolbarDragModeContractTests
{
    [Fact]
    public void ToolbarDrag_ShouldUseManagedMouseCaptureFlow_InsteadOfDragMove()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().NotContain("PaintActionInvoker.TryInvoke(DragMove);");
        source.Should().Contain("CaptureMouse();");
        source.Should().Contain("ReleaseMouseCapture();");
        source.Should().Contain("private void OnToolbarDragStart(");
        source.Should().Contain("private void OnToolbarDragMove(");
        source.Should().Contain("private void OnToolbarDragEnd(");
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
