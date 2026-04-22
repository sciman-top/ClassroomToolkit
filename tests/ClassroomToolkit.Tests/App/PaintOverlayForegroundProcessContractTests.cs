using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintOverlayForegroundProcessContractTests
{
    [Fact]
    public void PaintOverlayPresentation_ShouldResolveForegroundOwnership_WithoutDirectNativeCall()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var foreground = _presentationResolver.ResolveForeground();");
        source.Should().Contain("return foreground.Info.ProcessId == _currentProcessId;");
        source.Should().NotContain("GetWindowThreadProcessId(");
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
