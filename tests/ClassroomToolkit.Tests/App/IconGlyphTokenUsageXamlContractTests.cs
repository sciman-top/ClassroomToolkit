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

        aboutXaml.Should().Contain("Size_Icon_Glyph_MD");
        rollCallSettingsXaml.Should().Contain("Size_Icon_Glyph_SM");
        paintSettingsXaml.Should().Contain("Size_Icon_Glyph_MD");
        rollCallXaml.Should().Contain("Size_Icon_Glyph_MD");
        paintOverlayXaml.Should().Contain("Size_Icon_Glyph_MD");
        paintOverlayXaml.Should().Contain("Size_Icon_Glyph_LG");
    }

    private static string GetXamlPath(params string[] segments)
    {
        return TestPathHelper.ResolveAppPath(segments);
    }
}
