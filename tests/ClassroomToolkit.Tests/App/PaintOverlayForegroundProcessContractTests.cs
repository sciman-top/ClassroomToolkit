using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintOverlayForegroundProcessContractTests
{
    [Fact]
    public void PaintOverlayPresentation_ShouldResolveForegroundOwnership_WithoutDirectNativeCall()
    {
        var source = GetSource();

        source.Should().Contain("var foreground = _presentationResolver.ResolveForeground();");
        source.Should().Contain("return foreground.Info.ProcessId == _currentProcessId;");
        source.Should().NotContain("GetWindowThreadProcessId(");
    }

    private static string GetSource()
    {
        var paintRoot = TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(paintRoot, "PaintOverlayWindow.Presentation*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
