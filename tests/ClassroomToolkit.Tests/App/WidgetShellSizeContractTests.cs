using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSizeContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeShellSizeTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var required = new[]
        {
            "Size_Shell_TitleBar_Dialog",
            "Size_Shell_TitleBar_Management",
            "Size_Shell_TitleBar_Work",
            "Spacing_Shell_CloseButton_Dialog",
            "Spacing_Shell_CloseButton_Management"
        };

        foreach (var token in required)
        {
            xaml.Should().Contain($"x:Key=\"{token}\"");
        }
    }

    [Fact]
    public void WidgetStyles_ShouldConsumeShellSizeTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        xaml.Should().Contain("Style_DialogShellTitleBar");
        xaml.Should().Contain("Style_ManagementShellTitleBar");
        xaml.Should().Contain("Style_WorkShellTitleBar");
        xaml.Should().Contain("Style_DialogShellCloseButton");
        xaml.Should().Contain("Style_ManagementShellCloseButton");
        xaml.Should().Contain("Size_Shell_TitleBar_Dialog");
        xaml.Should().Contain("Size_Shell_TitleBar_Management");
        xaml.Should().Contain("Size_Shell_TitleBar_Work");
        xaml.Should().Contain("Spacing_Shell_CloseButton_Dialog");
        xaml.Should().Contain("Spacing_Shell_CloseButton_Management");
    }

    private static string GetWidgetStylesPath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "Assets",
            "Styles",
            "WidgetStyles.xaml");
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
