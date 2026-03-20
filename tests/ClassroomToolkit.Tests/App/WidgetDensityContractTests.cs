using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetDensityContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeDensityTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var requiredTokens = new[]
        {
            "Size_Button_Icon_Compact",
            "Size_Button_Icon_Regular",
            "Size_Button_Icon_Large",
            "Size_Button_Action_Regular",
            "Size_Button_Action_Height_Compact",
            "Size_Icon_Glyph_XS",
            "Size_Icon_Glyph_SM",
            "Size_Icon_Glyph_MD",
            "Size_Icon_Glyph_LG"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain($"x:Key=\"{token}\"");
        }
    }

    [Fact]
    public void WidgetStyles_ShouldUseDensityTokensInShellButtonStyles()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        xaml.Should().Contain("Style_IconButton");
        xaml.Should().Contain("Style_DialogShellCloseButton");
        xaml.Should().Contain("Style_OverlayShellCloseButton");
        xaml.Should().Contain("Size_Button_Icon_Compact");
        xaml.Should().Contain("Size_Button_Icon_Regular");
        xaml.Should().Contain("Size_Button_Icon_Large");
        xaml.Should().Contain("Size_Button_Action_Regular");
        xaml.Should().Contain("Size_Button_Action_Height_Compact");
        xaml.Should().Contain("Size_Icon_Glyph_XS");
        xaml.Should().Contain("Size_Icon_Glyph_SM");
        xaml.Should().Contain("Size_Icon_Glyph_MD");
        xaml.Should().Contain("Size_Icon_Glyph_LG");
    }

    private static string GetWidgetStylesPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Assets",
            "Styles",
            "WidgetStyles.xaml");
    }
}
