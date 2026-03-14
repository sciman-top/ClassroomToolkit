using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class SettingsDialogsXamlContractTests
{
    [Fact]
    public void SettingsDialogs_ShouldUseSemanticSurfaceTokens()
    {
        var paintSettingsXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallSettingsXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));

        paintSettingsXaml.Should().Contain("Brush_Surface_Secondary");
        paintSettingsXaml.Should().Contain("Brush_Surface_Primary");
        rollCallSettingsXaml.Should().Contain("Brush_Window_Atmosphere");
    }

    [Fact]
    public void SettingsDialogs_ShouldAvoidLegacyBackgroundKeys()
    {
        var paintSettingsXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallSettingsXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));

        paintSettingsXaml.Should().NotContain("Brush_Background_L2");
        paintSettingsXaml.Should().NotContain("Brush_Background_L3");
        rollCallSettingsXaml.Should().NotContain("Brush_Background_L2");
        rollCallSettingsXaml.Should().NotContain("Brush_Background_L3");
    }

    [Fact]
    public void SettingsDialogs_ShouldAvoidInlineDropShadowEffect()
    {
        var paintSettingsXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallSettingsXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));

        paintSettingsXaml.Should().NotContain("<DropShadowEffect");
        rollCallSettingsXaml.Should().NotContain("<DropShadowEffect");
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
