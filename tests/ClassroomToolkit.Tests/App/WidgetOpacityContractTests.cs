using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetOpacityContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeOpacityTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var requiredTokens = new[]
        {
            "Opacity_State_Disabled",
            "Opacity_State_Disabled_Soft",
            "Opacity_State_Pressed",
            "Opacity_State_Hover_Subtle",
            "Opacity_State_Icon_Disabled",
            "Opacity_Effect_InnerShine",
            "Opacity_Overlay_CloseButton",
            "Opacity_Overlay_SideRail",
            "Opacity_Overlay_HintBadge"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain($"x:Key=\"{token}\"");
        }
    }

    [Fact]
    public void WidgetStyles_CoreStyles_ShouldConsumeOpacityTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        xaml.Should().Contain("Opacity_Effect_InnerShine");
        xaml.Should().Contain("Opacity_State_Pressed");
        xaml.Should().Contain("Opacity_State_Disabled");
        xaml.Should().Contain("Opacity_State_Disabled_Soft");
        xaml.Should().Contain("Opacity_State_Hover_Subtle");
        xaml.Should().Contain("Opacity_State_Icon_Disabled");
        xaml.Should().Contain("Opacity_Overlay_CloseButton");
        xaml.Should().Contain("Opacity_Overlay_SideRail");
        xaml.Should().Contain("Opacity_Overlay_HintBadge");
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
