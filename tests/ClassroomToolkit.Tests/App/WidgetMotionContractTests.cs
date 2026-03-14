using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetMotionContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeMotionDurationTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());
        xaml.Should().Contain("x:Key=\"Motion_Duration_Fast\"");
    }

    [Fact]
    public void WidgetStyles_ShouldConsumeMotionDurationTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());
        xaml.Should().Contain("Duration=\"{StaticResource Motion_Duration_Fast}\"");
        xaml.Should().NotContain("Duration=\"0:0:0.2\"");
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
