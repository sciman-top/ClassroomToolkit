using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class MainWindowXamlContractTests
{
    [Fact]
    public void MainWindow_ShouldKeepLauncherPrimaryStructure()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        xaml.Should().Contain("Style_WorkShellWindowBorder");
        xaml.Should().Contain("x:Name=\"PaintButton\"");
        xaml.Should().Contain("x:Name=\"RollCallButton\"");
        xaml.Should().Contain("x:Name=\"MinimizeButton\"");
        xaml.Should().Contain("x:Name=\"SettingsButton\"");
        xaml.Should().Contain("x:Name=\"AboutButton\"");
        xaml.Should().Contain("x:Name=\"ExitButton\"");
    }

    [Fact]
    public void MainWindow_ShouldConsumeStageASemanticTokens()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        var requiredTokens = new[]
        {
            "Brush_Surface_Primary",
            "Brush_Surface_Secondary",
            "Brush_InputBackground",
            "Brush_Border_Subtle",
            "Brush_Border_Strong",
            "Shadow_Glow_Primary",
            "Shadow_Dialog"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain(token, $"MainWindow should consume Stage A token '{token}'");
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
