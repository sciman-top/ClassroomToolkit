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
        aboutXaml.Should().Contain("Style_SettingCardBorder");
        studentListXaml.Should().Contain("Style_StudentCardButton");
        studentListXaml.Should().Contain("Brush_InputBackground");
        imageManagerXaml.Should().Contain("Brush_Surface_Secondary");
        imageManagerXaml.Should().Contain("Brush_InputBackground");
        imageManagerXaml.Should().Contain("Style_ManagementThumbnailListViewItem");
        imageManagerXaml.Should().Contain("Size_Icon_Glyph_MD");
        diagnosticsXaml.Should().Contain("Brush_InputBackground");
        diagnosticsXaml.Should().Contain("Size_Icon_Glyph_MD");
    }

    [Fact]
    public void ManagementWindows_ShouldPreferSharedCardAndListItemStyles()
    {
        var studentListXaml = File.ReadAllText(GetXamlPath("StudentListDialog.xaml"));
        var imageManagerXaml = File.ReadAllText(GetXamlPath("Photos", "ImageManagerWindow.xaml"));

        studentListXaml.Should().Contain("Style_StudentCardButton");
        studentListXaml.Should().NotContain("x:Key=\"Style_StudentCard_Modern\"");
        imageManagerXaml.Should().Contain("Style_ManagementThumbnailListViewItem");
        imageManagerXaml.Should().NotContain("x:Key=\"ImageTileListViewItemStyle\"");
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
        return TestPathHelper.ResolveAppPath(segments);
    }
}
