using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSpacingRadiusContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeShellSpacingAndRadiusTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var requiredTokens = new[]
        {
            "Spacing_Shell_DialogMargin",
            "Spacing_Shell_TitleInset",
            "Spacing_Shell_ActionBar",
            "Spacing_Shell_ActionGap",
            "Spacing_Shell_WorkTitleBar",
            "Spacing_Shell_WorkContent",
            "Spacing_Shell_WorkBottomBar",
            "Spacing_Shell_ManagementContent",
            "Radius_Shell_Dialog",
            "Radius_Shell_Management",
            "Radius_Shell_Work",
            "Radius_Shell_WorkBottomBar",
            "Radius_Shell_OverlaySideRail",
            "Radius_Shell_OverlayHintBadge"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain($"x:Key=\"{token}\"");
        }
    }

    [Fact]
    public void WidgetStyles_ShellStyles_ShouldConsumeSpacingAndRadiusTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var requiredRefs = new[]
        {
            "Radius_Shell_Dialog",
            "Spacing_Shell_DialogMargin",
            "Spacing_Shell_TitleInset",
            "Spacing_Shell_ActionBar",
            "Spacing_Shell_ActionGap",
            "Radius_Shell_Management",
            "Spacing_Shell_ManagementContent",
            "Radius_Shell_Work",
            "Spacing_Shell_WorkTitleBar",
            "Spacing_Shell_WorkContent",
            "Radius_Shell_WorkBottomBar",
            "Spacing_Shell_WorkBottomBar",
            "Radius_Shell_OverlaySideRail",
            "Radius_Shell_OverlayHintBadge"
        };

        foreach (var token in requiredRefs)
        {
            xaml.Should().Contain(token);
        }
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
