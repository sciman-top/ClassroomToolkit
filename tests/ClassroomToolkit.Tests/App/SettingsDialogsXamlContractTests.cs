using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class SettingsDialogsXamlContractTests
{
    [Fact]
    public void SettingsDialogs_ShouldUseSemanticSurfaceTokens()
    {
        var paintSettingsXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallSettingsXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));
        var classSelectXaml = File.ReadAllText(GetXamlPath("ClassSelectDialog.xaml"));
        var inkSettingsXaml = File.ReadAllText(GetXamlPath("Ink", "InkSettingsDialog.xaml"));
        var remoteKeyXaml = File.ReadAllText(GetXamlPath("RemoteKeyDialog.xaml"));
        var autoExitXaml = File.ReadAllText(GetXamlPath("AutoExitDialog.xaml"));
        var timerSetXaml = File.ReadAllText(GetXamlPath("TimerSetDialog.xaml"));

        paintSettingsXaml.Should().Contain("Brush_Surface_Secondary");
        paintSettingsXaml.Should().Contain("Style_DialogShellWindowBorder");
        paintSettingsXaml.Should().Contain("Style_SettingCardBorder");
        rollCallSettingsXaml.Should().Contain("Brush_Window_Atmosphere");
        rollCallSettingsXaml.Should().Contain("Style_DialogShellWindowBorder");
        rollCallSettingsXaml.Should().Contain("Style_SettingCardBorder");
        classSelectXaml.Should().Contain("Style_DialogShellWindowBorder");
        inkSettingsXaml.Should().Contain("Style_DialogShellWindowBorder");
        inkSettingsXaml.Should().Contain("Size_Icon_Glyph_MD");
        remoteKeyXaml.Should().Contain("Style_DialogShellCloseButton");
        autoExitXaml.Should().Contain("Size_Icon_Glyph_MD");
        timerSetXaml.Should().Contain("Style_DialogShellCloseButton");
        timerSetXaml.Should().Contain("Style_SettingCardBorder");
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
