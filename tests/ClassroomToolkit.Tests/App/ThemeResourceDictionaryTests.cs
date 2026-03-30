using System.Windows;
using System.Windows.Markup;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class ThemeResourceDictionaryTests
{
    [Fact]
    public void ColorsDictionary_ShouldExposeStageATokens()
    {
        var dictionary = LoadColorsDictionary();

        var requiredKeys = new[]
        {
            "Color_Bg_App",
            "Color_Bg_Surface_1",
            "Color_Bg_Surface_2",
            "Color_Bg_Input",
            "Color_Bg_Overlay",
            "Color_Text_Primary",
            "Color_Text_Secondary",
            "Color_Text_Tertiary",
            "Color_Accent_Primary",
            "Color_Accent_Teaching",
            "Color_Accent_Success",
            "Color_Accent_Warning",
            "Color_Accent_Danger",
            "Color_Border_Subtle",
            "Color_Border_Strong",
            "Color_Border_Focus",
            "Brush_AppBackground",
            "Brush_Surface_Primary",
            "Brush_Surface_Secondary",
            "Brush_InputBackground",
            "Brush_OverlayMask",
            "Brush_Primary",
            "Brush_Teaching",
            "Brush_Success",
            "Brush_Warning",
            "Brush_Danger",
            "Gradient_Primary_Subtle",
            "Gradient_Teaching_Subtle",
            "Gradient_Timer_Display",
            "Shadow_Card",
            "Shadow_Dialog",
            "Shadow_Floating",
            "Shadow_Glow_Primary",
            "Shadow_Glow_Teaching"
        };

        foreach (var key in requiredKeys)
        {
            dictionary.Contains(key).Should().BeTrue($"theme token '{key}' should exist");
        }
    }

    [Fact]
    public void ColorsDictionary_ShouldKeepLegacyAliasesForCurrentWindows()
    {
        var dictionary = LoadColorsDictionary();

        var legacyKeys = new[]
        {
            "Brush_Background",
            "Brush_Background_L2",
            "Brush_Background_L3",
            "Brush_Window_Atmosphere",
            "Brush_Glass_Surface",
            "Brush_Glass_Border",
            "Brush_Surface_Hover",
            "Brush_Surface_Active",
            "Brush_Border",
            "Brush_Border_Light",
            "Brush_Border_Glass",
            "Brush_Text_Primary",
            "Brush_Text_Secondary",
            "Brush_Text_Tertiary",
            "Brush_Danger",
            "Brush_Success",
            "Brush_Warning",
            "Gradient_Primary",
            "Gradient_Primary_Hover",
            "Gradient_Danger",
            "Gradient_Warning",
            "Gradient_RollCall_Card",
            "Brush_Timer_Neon",
            "Shadow_Dialog_Heavy",
            "Shadow_Card_Subtle"
        };

        foreach (var key in legacyKeys)
        {
            dictionary.Contains(key).Should().BeTrue($"legacy theme key '{key}' should remain available during Stage A");
        }
    }

    private static ResourceDictionary LoadColorsDictionary()
    {
        var colorsPath = TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Assets",
            "Styles",
            "Colors.xaml");

        using var stream = File.OpenRead(colorsPath);
        return (ResourceDictionary)XamlReader.Load(stream);
    }
}
