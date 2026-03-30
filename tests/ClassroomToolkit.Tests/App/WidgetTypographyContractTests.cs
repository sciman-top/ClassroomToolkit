using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetTypographyContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeTypographyTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var requiredTokens = new[]
        {
            "FontSize_Body_S",
            "FontSize_Body_M",
            "FontSize_Title_Dialog",
            "FontSize_Title_Management"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain($"x:Key=\"{token}\"");
        }
    }

    [Fact]
    public void ShellTypographyStyles_ShouldConsumeTypographyTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        xaml.Should().Contain("Style_ButtonFamilyBase");
        xaml.Should().Contain("Style_DialogShellTitleText");
        xaml.Should().Contain("Style_ManagementShellTitleText");
        xaml.Should().Contain("Style_ManagementShellSubtitleText");
        xaml.Should().Contain("Style_ManagementShellFooterText");
        xaml.Should().Contain("FontSize_Body_S");
        xaml.Should().Contain("FontSize_Body_M");
        xaml.Should().Contain("FontSize_Title_Dialog");
        xaml.Should().Contain("FontSize_Title_Management");
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
