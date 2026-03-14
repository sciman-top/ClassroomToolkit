using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class ManagementWindowsXamlContractTests
{
    [Fact]
    public void ManagementWindows_ShouldConsumeSemanticSurfaceTokens()
    {
        var aboutXaml = File.ReadAllText(GetXamlPath("AboutDialog.xaml"));
        var studentListXaml = File.ReadAllText(GetXamlPath("StudentListDialog.xaml"));
        var imageManagerXaml = File.ReadAllText(GetXamlPath("Photos", "ImageManagerWindow.xaml"));
        var diagnosticsXaml = File.ReadAllText(GetXamlPath("Diagnostics", "DiagnosticsDialog.xaml"));

        aboutXaml.Should().Contain("Brush_Surface_Secondary");
        studentListXaml.Should().Contain("Brush_Surface_Secondary");
        studentListXaml.Should().Contain("Brush_InputBackground");
        imageManagerXaml.Should().Contain("Brush_Surface_Secondary");
        imageManagerXaml.Should().Contain("Brush_InputBackground");
        diagnosticsXaml.Should().Contain("Brush_Surface_Secondary");
    }

    [Fact]
    public void ManagementWindows_ShouldAvoidLegacyBackgroundKeys()
    {
        var aboutXaml = File.ReadAllText(GetXamlPath("AboutDialog.xaml"));
        var studentListXaml = File.ReadAllText(GetXamlPath("StudentListDialog.xaml"));
        var imageManagerXaml = File.ReadAllText(GetXamlPath("Photos", "ImageManagerWindow.xaml"));
        var diagnosticsXaml = File.ReadAllText(GetXamlPath("Diagnostics", "DiagnosticsDialog.xaml"));

        aboutXaml.Should().NotContain("Brush_Background_L2");
        studentListXaml.Should().NotContain("Brush_Background_L2");
        studentListXaml.Should().NotContain("Brush_Background_L3");
        imageManagerXaml.Should().NotContain("Brush_Background_L2");
        imageManagerXaml.Should().NotContain("Brush_Background_L3");
        diagnosticsXaml.Should().NotContain("Brush_Background_L2");
    }

    [Fact]
    public void AboutDialog_ShouldAvoidInlineDropShadowEffect()
    {
        var xaml = File.ReadAllText(GetXamlPath("AboutDialog.xaml"));
        xaml.Should().NotContain("<DropShadowEffect");
        xaml.Should().Contain("Shadow_Glow_Primary");
    }

    private static string GetXamlPath(params string[] segments)
    {
        var root = FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName;
        var fullSegments = new List<string> { root, "src", "ClassroomToolkit.App" };
        fullSegments.AddRange(segments);
        return Path.Combine(fullSegments.ToArray());
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
