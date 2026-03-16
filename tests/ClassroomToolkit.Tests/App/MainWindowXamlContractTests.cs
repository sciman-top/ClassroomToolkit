using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class MainWindowXamlContractTests
{
    [Fact]
    public void MainWindow_ShouldKeepLauncherPrimaryStructure()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        xaml.Should().Contain("Style_WorkShellWindowBorder");
        xaml.Should().Contain("Style_WorkShellHeroTileButton");
        xaml.Should().Contain("Style_WorkShellMiniButton");
        xaml.Should().Contain("Style_WorkShellMiniDangerButton");
        xaml.Should().Contain("x:Name=\"PaintButton\"");
        xaml.Should().Contain("x:Name=\"RollCallButton\"");
        xaml.Should().Contain("x:Name=\"MinimizeButton\"");
        xaml.Should().Contain("x:Name=\"SettingsButton\"");
        xaml.Should().Contain("x:Name=\"AboutButton\"");
        xaml.Should().Contain("x:Name=\"ExitButton\"");
        xaml.Should().NotContain("x:Key=\"Style_HeroTile\"");
        xaml.Should().NotContain("x:Key=\"Style_MiniTool\"");
        xaml.Should().NotContain("x:Key=\"Style_MiniDanger\"");
    }

    [Fact]
    public void MainWindow_ShouldConsumeStageASemanticTokens()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        var requiredReferences = new[]
        {
            "Style_WorkShellHeroTileButton",
            "Style_WorkShellMiniButton",
            "Style_WorkShellMiniDangerButton",
            "Brush_Border_Strong",
            "Shadow_Dialog"
        };

        foreach (var token in requiredReferences)
        {
            xaml.Should().Contain(token, $"MainWindow should consume shared Stage A reference '{token}'");
        }
    }

    [Fact]
    public void MainWindow_ShouldAvoidInlineDropShadowEffects()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        xaml.Should().NotContain("<DropShadowEffect", "MainWindow should use shared shadow resources instead of inline shadow definitions");
    }

    private static string GetMainWindowXamlPath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml");
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
