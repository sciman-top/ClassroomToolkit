using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputResumeCaptureContractTests
{
    [Fact]
    public void ResumeCrossPageInputOperationAfterSwitch_ShouldReacquireCapture_ForEraserContinuation()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (executionPlan.Action == CrossPageInputResumeAction.BeginEraser)");
        source.Should().Contain("CapturePointerInput();");
        source.Should().Contain("BeginEraser(input.Position);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.cs");
    }
}
