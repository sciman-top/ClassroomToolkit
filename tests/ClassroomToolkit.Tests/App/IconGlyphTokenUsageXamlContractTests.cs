using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class IconGlyphTokenUsageXamlContractTests
{
    [Fact]
    public void CoreWindows_ShouldUseIconGlyphSizeTokens()
    {
        var aboutXaml = File.ReadAllText(GetXamlPath("AboutDialog.xaml"));
        var rollCallSettingsXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));
        var paintSettingsXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallXaml = File.ReadAllText(GetXamlPath("RollCallWindow.xaml"));
        var paintOverlayXaml = File.ReadAllText(GetXamlPath("Paint", "PaintOverlayWindow.xaml"));

        aboutXaml.Should().Contain("Size_Icon_Glyph_XS");
        rollCallSettingsXaml.Should().Contain("Size_Icon_Glyph_SM");
        paintSettingsXaml.Should().Contain("Size_Icon_Glyph_MD");
        rollCallXaml.Should().Contain("Size_Icon_Glyph_MD");
        paintOverlayXaml.Should().Contain("Size_Icon_Glyph_MD");
        paintOverlayXaml.Should().Contain("Size_Icon_Glyph_LG");
    }

    private static string GetXamlPath(params string[] segments)
    {
        var root = FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName;
        var full = new List<string> { root, "src", "ClassroomToolkit.App" };
        full.AddRange(segments);
        return Path.Combine(full.ToArray());
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
