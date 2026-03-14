using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetBorderThicknessContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeBorderThicknessTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());
        xaml.Should().Contain("x:Key=\"BorderThickness_Regular\"");
        xaml.Should().Contain("x:Key=\"BorderThickness_Emphasis\"");
    }

    [Fact]
    public void WidgetStyles_CoreStyles_ShouldConsumeBorderThicknessTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());
        xaml.Should().Contain("BorderThickness_Regular");
        xaml.Should().Contain("BorderThickness_Emphasis");
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
