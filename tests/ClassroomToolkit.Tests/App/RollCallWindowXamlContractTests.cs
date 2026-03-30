using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallWindowXamlContractTests
{
    [Fact]
    public void RollCallWindow_ShouldKeepWorkShellCoreStructure()
    {
        var xaml = File.ReadAllText(GetRollCallWindowXamlPath());

        xaml.Should().Contain("Style_WorkShellWindowBorder");
        xaml.Should().Contain("x:Name=\"TitleBarRoot\"");
        xaml.Should().Contain("x:Name=\"RollNameCard\"");
        xaml.Should().Contain("x:Name=\"TimerCard\"");
        xaml.Should().Contain("x:Name=\"BottomBarRoot\"");
        xaml.Should().Contain("x:Name=\"RollCallActionsPanel\"");
        xaml.Should().Contain("x:Name=\"TimerActionsPanel\"");
    }

    [Fact]
    public void RollCallWindow_ShouldConsumeStageASemanticTokens()
    {
        var xaml = File.ReadAllText(GetRollCallWindowXamlPath());

        var requiredTokens = new[]
        {
            "Brush_Surface_Primary",
            "Brush_Surface_Secondary",
            "Brush_InputBackground",
            "Brush_OverlayMask",
            "Brush_Border_Subtle",
            "Brush_Border_Strong",
            "Brush_Border_Focus",
            "Brush_Teaching",
            "Gradient_Timer_Display",
            "Shadow_Dialog",
            "Shadow_Floating",
            "Shadow_Glow_Teaching"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain(token, $"RollCallWindow should consume Stage A token '{token}'");
        }
    }

    [Fact]
    public void RollCallWindow_ShouldAvoidInlineDropShadowEffects()
    {
        var xaml = File.ReadAllText(GetRollCallWindowXamlPath());
        xaml.Should().NotContain("<DropShadowEffect", "RollCallWindow should use shared shadow resources");
    }

    private static string GetRollCallWindowXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.xaml");
    }
}
