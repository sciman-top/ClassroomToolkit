using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetStylesContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeStageAShellStyleKeys()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

        var requiredKeys = new[]
        {
            "Style_PrimaryButton",
            "Style_SecondaryButton",
            "Style_DangerButton",
            "Style_IconButton",
            "Style_IconButton_Active",
            "Style_WorkShellHeroTileButton",
            "Style_WorkShellMiniButton",
            "Style_WorkShellMiniDangerButton",
            "Style_ColorBubbleToggle",
            "Style_ColorPaletteButton",
            "Style_StudentCardButton",
            "Style_SettingCardBorder",
            "Style_ManagementThumbnailListViewItem",
            "Style_DialogShellWindowBorder",
            "Style_DialogShellTitleBar",
            "Style_ManagementShellWindowBorder",
            "Style_WorkShellWindowBorder",
            "Style_OverlayShellCloseButton",
            "Style_OverlayShellSideRail",
            "Style_OverlayShellHintBadge",
            "Style_BubbleShellRoot",
            "Style_BubbleShellItem",
            "Style_BubbleShellSelectedItem",
            "Style_FullscreenShellSideRail",
            "Style_FullscreenShellHintBadge"
        };

        foreach (var key in requiredKeys)
        {
            xaml.Should().Contain($"x:Key=\"{key}\"");
        }
    }

    [Fact]
    public void WidgetStyles_ShouldReferenceStageASemanticTokens()
    {
        var xaml = File.ReadAllText(GetWidgetStylesPath());

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
            "Gradient_Primary_Subtle",
            "Gradient_Teaching_Subtle",
            "Shadow_Dialog",
            "Shadow_Floating",
            "Shadow_Glow_Primary"
        };

        foreach (var token in requiredTokens)
        {
            xaml.Should().Contain(token, $"WidgetStyles should start consuming Stage A semantic token '{token}'");
        }
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
