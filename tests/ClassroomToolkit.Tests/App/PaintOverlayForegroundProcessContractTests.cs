using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintOverlayForegroundProcessContractTests
{
    [Fact]
    public void PaintOverlayPresentation_ShouldValidateGetWindowThreadProcessIdResult()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var threadId = Interop.NativeMethods.GetWindowThreadProcessId(foreground, out var processId);");
        source.Should().Contain("if (threadId == 0 || processId == 0)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
    }
}
