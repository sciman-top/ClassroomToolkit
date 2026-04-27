using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class ThemeResourceDictionaryTests
{
    [Fact]
    public void ColorsDictionary_ShouldExposeSharedTokenShapeAndMappings()
    {
        var dictionary = LoadColorsDictionary();

        AssertKeysExist(dictionary, SemanticColorKeys);
        AssertKeysExist(dictionary, SemanticBrushKeys);
        AssertKeysExist(dictionary, SemanticGradientKeys);
        AssertKeysExist(dictionary, SemanticShadowKeys);

        AssertResourceTypes<Color>(dictionary, SemanticColorKeys);
        AssertResourceTypes<SolidColorBrush>(dictionary, SemanticBrushKeys);
        AssertResourceTypes<LinearGradientBrush>(dictionary, SemanticGradientKeys);
        AssertResourceTypes<DropShadowEffect>(dictionary, SemanticShadowKeys);

        AssertBrushMatchesColor(dictionary, "Brush_AppBackground", "Color_Bg_App");
        AssertBrushMatchesColor(dictionary, "Brush_Surface_Primary", "Color_Bg_Surface_1");
        AssertBrushMatchesColor(dictionary, "Brush_Surface_Secondary", "Color_Bg_Surface_2");
        AssertBrushMatchesColor(dictionary, "Brush_InputBackground", "Color_Bg_Input");
        AssertBrushMatchesColor(dictionary, "Brush_OverlayMask", "Color_Bg_Overlay");
        AssertBrushMatchesColor(dictionary, "Brush_GlassSurface", "Color_Bg_Glass");
        AssertBrushMatchesColor(dictionary, "Brush_Primary", "Color_Accent_Primary");
        AssertBrushMatchesColor(dictionary, "Brush_Primary_Hover", "Color_Accent_Primary_Hover");
        AssertBrushMatchesColor(dictionary, "Brush_Primary_Dark", "Color_Accent_Primary_Deep");
        AssertBrushMatchesColor(dictionary, "Brush_Primary_Light", "Color_Accent_Primary_Hover");
        AssertBrushMatchesColor(dictionary, "Brush_Teaching", "Color_Accent_Teaching");
        AssertBrushMatchesColor(dictionary, "Brush_Teaching_Deep", "Color_Accent_Teaching_Deep");
        AssertBrushMatchesColor(dictionary, "Brush_Success", "Color_Accent_Success");
        AssertBrushMatchesColor(dictionary, "Brush_Warning", "Color_Accent_Warning");
        AssertBrushMatchesColor(dictionary, "Brush_Danger", "Color_Accent_Danger");
        AssertBrushMatchesColor(dictionary, "Brush_Accent_Teal", "Color_Accent_Primary_Hover");
        AssertBrushMatchesColor(dictionary, "Brush_Accent_Violet", "Color_Accent_Violet_Muted");
        AssertBrushMatchesColor(dictionary, "Brush_Accent_Amber", "Color_Accent_Amber");
        AssertBrushMatchesColor(dictionary, "Brush_Border_Subtle", "Color_Border_Subtle");
        AssertBrushMatchesColor(dictionary, "Brush_Border_Strong", "Color_Border_Strong");
        AssertBrushMatchesColor(dictionary, "Brush_Border_Focus", "Color_Border_Focus");
        AssertBrushMatchesColor(dictionary, "Brush_Border_Active", "Color_Border_Active");
        AssertBrushMatchesColor(dictionary, "Brush_Border_Glass", "Color_Border_Glass");
        AssertBrushMatchesColor(dictionary, "Brush_Timer_Display", "Color_Timer_Display");
        AssertBrushMatchesColor(dictionary, "Brush_Pen_Red", "Color_Pen_Red");
        AssertBrushMatchesColor(dictionary, "Brush_Pen_Blue", "Color_Pen_Blue");

        AssertGradientStructure(dictionary, "Gradient_Primary_Subtle", 2);
        AssertGradientStructure(dictionary, "Gradient_Teaching_Subtle", 2);
        AssertGradientStructure(dictionary, "Gradient_Timer_Display", 3);
        AssertGradientStructure(dictionary, "Gradient_Hero_Glow", 3);
        AssertGradientStructure(dictionary, "Gradient_Shell_Surface", 3);
        AssertGradientStructure(dictionary, "Gradient_Card_Surface", 2);
        AssertGradientStructure(dictionary, "Gradient_Panel_Surface", 2);

        AssertShadowStructure(dictionary, "Shadow_Card");
        AssertShadowStructure(dictionary, "Shadow_Card_Subtle");
        AssertShadowStructure(dictionary, "Shadow_Dialog");
        AssertShadowStructure(dictionary, "Shadow_Floating");
        AssertShadowStructure(dictionary, "Shadow_Glow_Primary");
        AssertShadowStructure(dictionary, "Shadow_Glow_Teaching");
        AssertShadowStructure(dictionary, "Shadow_Glow_Hero");

        AssertShadowRelativeIntensity(dictionary, "Shadow_Card_Subtle", "Shadow_Card");
        AssertShadowRelativeIntensity(dictionary, "Shadow_Card", "Shadow_Dialog");
        AssertShadowRelativeIntensity(dictionary, "Shadow_Floating", "Shadow_Dialog");
        AssertGlowShadowContrast(dictionary, "Shadow_Glow_Hero", "Shadow_Glow_Primary");
    }

    [Fact]
    public void ColorsDictionary_ShouldKeepLegacyCompatibilityContract()
    {
        var dictionary = LoadColorsDictionary();

        AssertKeysExist(dictionary, LegacyColorKeys);
        AssertKeysExist(dictionary, LegacyBrushKeys);
        AssertKeysExist(dictionary, LegacyGradientKeys);
        AssertKeysExist(dictionary, LegacyShadowKeys);

        AssertResourceTypes<Color>(dictionary, LegacyColorKeys);
        AssertResourceTypes<SolidColorBrush>(dictionary, LegacyBrushKeys);
        AssertResourceTypes<LinearGradientBrush>(dictionary, LegacyGradientKeys);
        AssertResourceTypes<DropShadowEffect>(dictionary, LegacyShadowKeys);

        AssertBrushMatchesColor(dictionary, "Brush_Background", "Color_Bg_App");
        AssertBrushMatchesColor(dictionary, "Brush_Window_Atmosphere", "Color_Bg_App");
        AssertBrushMatchesColor(dictionary, "Brush_Background_L2", "Color_Bg_Surface_1");
        AssertBrushMatchesColor(dictionary, "Brush_Background_L3", "Color_Bg_Input");
        AssertBrushMatchesColor(dictionary, "Brush_Glass_Surface", "Color_Bg_Glass");
        AssertBrushMatchesColor(dictionary, "Brush_Border", "Color_Border_Subtle");
        AssertBrushMatchesColor(dictionary, "Brush_Border_Light", "Color_Border_Strong");
        AssertBrushMatchesColor(dictionary, "Brush_Text_Primary", "Color_Text_Primary");
        AssertBrushMatchesColor(dictionary, "Brush_Text_Secondary", "Color_Text_Secondary");
        AssertBrushMatchesColor(dictionary, "Brush_Text_Tertiary", "Color_Text_Tertiary");
        AssertBrushMatchesColor(dictionary, "Brush_Danger", "Color_Accent_Danger");
        AssertBrushMatchesColor(dictionary, "Brush_Success", "Color_Accent_Success");
        AssertBrushMatchesColor(dictionary, "Brush_Warning", "Color_Accent_Warning");
        AssertBrushMatchesColor(dictionary, "Brush_Timer_Neon", "Color_Timer_Display");
        AssertBrushMatchesColor(dictionary, "Brush_Accent_Teal", "Color_Accent_Primary_Hover");
        AssertBrushMatchesColor(dictionary, "Brush_Primary_Light", "Color_Accent_Primary_Hover");
        AssertBrushMatchesColor(dictionary, "Brush_Primary_Dark", "Color_Accent_Primary_Deep");

        AssertGradientUsesColorKeys(dictionary, "Gradient_Primary", "Color_Accent_Primary", "Color_Primary_GradientEnd");
        AssertGradientUsesColorKeys(dictionary, "Gradient_Success", "Color_Success_GradientStart", "Color_Accent_Success");
        AssertGradientUsesColorKeys(dictionary, "Gradient_Danger", "Color_Accent_Danger", "Color_Danger_GradientEnd");
        AssertGradientUsesColorKeys(dictionary, "Gradient_Warning", "Color_Accent_Warning", "Color_Warning_GradientEnd");
        AssertGradientStartsWithColorKey(dictionary, "Gradient_Primary_Hover", "Color_Accent_Primary_Hover");
        AssertGradientHasOpaqueStops(dictionary, "Gradient_Primary_Hover", 2);
        AssertGradientHasOpaqueStops(dictionary, "Gradient_RollCall_Card", 3);
        AssertGradientHasOpaqueStops(dictionary, "Gradient_Launcher", 2);

        AssertShadowDepthZero(dictionary, "Shadow_Primary_Glow");
        AssertShadowDepthZero(dictionary, "Shadow_Danger_Glow");
        AssertShadowDepthZero(dictionary, "Shadow_Glass_Glow");
        AssertShadowMatchesColorKey(dictionary, "Shadow_Primary_Glow", "Color_Accent_Primary");
        AssertShadowMatchesColorKey(dictionary, "Shadow_Danger_Glow", "Color_Accent_Danger");
        AssertShadowRelativeIntensity(dictionary, "Shadow_Glass_Glow", "Shadow_Primary_Glow");
        AssertShadowRelativeIntensity(dictionary, "Shadow_Dialog", "Shadow_Dialog_Heavy");
    }

    private static readonly string[] SemanticColorKeys =
    {
        "Color_Bg_App",
        "Color_Bg_Surface_1",
        "Color_Bg_Surface_2",
        "Color_Bg_Input",
        "Color_Bg_Overlay",
        "Color_Bg_Glass",
        "Color_Text_Primary",
        "Color_Text_Secondary",
        "Color_Text_Tertiary",
        "Color_Text_Black",
        "Color_Accent_Primary",
        "Color_Accent_Primary_Hover",
        "Color_Accent_Primary_Deep",
        "Color_Accent_Teaching",
        "Color_Accent_Teaching_Deep",
        "Color_Accent_Success",
        "Color_Accent_Warning",
        "Color_Accent_Danger",
        "Color_Accent_Violet_Muted",
        "Color_Border_Subtle",
        "Color_Border_Strong",
        "Color_Border_Focus",
        "Color_Border_Active",
        "Color_Border_Glass",
        "Color_Timer_Display",
        "Color_Pen_Red",
        "Color_Pen_Blue"
    };

    private static readonly string[] SemanticBrushKeys =
    {
        "Brush_AppBackground",
        "Brush_Surface_Primary",
        "Brush_Surface_Secondary",
        "Brush_InputBackground",
        "Brush_OverlayMask",
        "Brush_GlassSurface",
        "Brush_Text_Primary",
        "Brush_Text_Secondary",
        "Brush_Text_Tertiary",
        "Brush_Text_Black",
        "Brush_Primary",
        "Brush_Primary_Hover",
        "Brush_Primary_Dark",
        "Brush_Primary_Light",
        "Brush_Teaching",
        "Brush_Teaching_Deep",
        "Brush_Success",
        "Brush_Warning",
        "Brush_Danger",
        "Brush_Accent_Teal",
        "Brush_Accent_Violet",
        "Brush_Accent_Amber",
        "Brush_Border_Subtle",
        "Brush_Border_Strong",
        "Brush_Border_Focus",
        "Brush_Border_Active",
        "Brush_Border_Glass",
        "Brush_Timer_Display",
        "Brush_Pen_Red",
        "Brush_Pen_Blue"
    };

    private static readonly string[] SemanticGradientKeys =
    {
        "Gradient_Primary_Subtle",
        "Gradient_Teaching_Subtle",
        "Gradient_Timer_Display",
        "Gradient_Hero_Glow",
        "Gradient_Shell_Surface",
        "Gradient_Card_Surface",
        "Gradient_Panel_Surface"
    };

    private static readonly string[] SemanticShadowKeys =
    {
        "Shadow_Card",
        "Shadow_Card_Subtle",
        "Shadow_Dialog",
        "Shadow_Floating",
        "Shadow_Glow_Primary",
        "Shadow_Glow_Teaching",
        "Shadow_Glow_Hero"
    };

    private static readonly string[] LegacyColorKeys =
    {
        "Color_Bg_Deep",
        "Color_Bg_Surface",
        "Color_Primary",
        "Color_Primary_GradientStart",
        "Color_Primary_GradientEnd",
        "Color_Secondary",
        "Color_Danger",
        "Color_Danger_GradientEnd",
        "Color_Success",
        "Color_Success_GradientStart",
        "Color_Warning",
        "Color_Warning_GradientEnd",
        "Color_Accent_Teal",
        "Color_Accent_Violet",
        "Color_Accent_Amber",
        "Color_Timer_Neon"
    };

    private static readonly string[] LegacyBrushKeys =
    {
        "Brush_Background",
        "Brush_Window_Atmosphere",
        "Brush_Background_L2",
        "Brush_Background_L3",
        "Brush_Glass_Surface",
        "Brush_Glass_Border",
        "Brush_Surface_Hover",
        "Brush_Surface_Active",
        "Brush_Surface_Tint",
        "Brush_Border",
        "Brush_Border_Light",
        "Brush_Border_Glass",
        "Brush_Text_Primary",
        "Brush_Text_Secondary",
        "Brush_Text_Tertiary",
        "Brush_Danger",
        "Brush_Success",
        "Brush_Warning",
        "Brush_Accent_Teal",
        "Brush_Primary_Light",
        "Brush_Primary_Dark",
        "Brush_Timer_Neon",
        "Brush_Danger_Surface",
        "Brush_Overlay_Dark"
    };

    private static readonly string[] LegacyGradientKeys =
    {
        "Gradient_Primary",
        "Gradient_Primary_Hover",
        "Gradient_Success",
        "Gradient_Danger",
        "Gradient_Warning",
        "Gradient_RollCall_Card",
        "Gradient_Launcher"
    };

    private static readonly string[] LegacyShadowKeys =
    {
        "Shadow_Primary_Glow",
        "Shadow_Danger_Glow",
        "Shadow_Glass_Glow",
        "Shadow_Dialog_Heavy",
        "Shadow_Card_Subtle"
    };

    private static void AssertKeysExist(ResourceDictionary dictionary, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            dictionary.Contains(key).Should().BeTrue($"theme key '{key}' should remain available");
        }
    }

    private static void AssertResourceTypes<T>(ResourceDictionary dictionary, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            dictionary[key].Should().BeOfType<T>($"theme key '{key}' should be a {typeof(T).Name}");
        }
    }

    private static void AssertBrushMatchesColor(ResourceDictionary dictionary, string brushKey, string colorKey)
    {
        var brush = GetBrush(dictionary, brushKey);
        var color = GetColor(dictionary, colorKey);
        brush.Color.Should().Be(color, $"brush '{brushKey}' should inherit from color '{colorKey}'");
    }

    private static void AssertGradientStructure(ResourceDictionary dictionary, string key, int expectedStops)
    {
        var brush = GetGradient(dictionary, key);
        brush.GradientStops.Should().HaveCount(expectedStops, $"gradient '{key}' should keep its stop count");
        brush.GradientStops.Select(stop => stop.Color.A).Should().OnlyContain(alpha => alpha == 255, $"gradient '{key}' should stay opaque");
    }

    private static void AssertGradientUsesColorKeys(ResourceDictionary dictionary, string key, string startColorKey, string endColorKey)
    {
        var brush = GetGradient(dictionary, key);
        brush.GradientStops.Should().HaveCount(2);
        brush.GradientStops[0].Color.Should().Be(GetColor(dictionary, startColorKey));
        brush.GradientStops[1].Color.Should().Be(GetColor(dictionary, endColorKey));
    }

    private static void AssertGradientStartsWithColorKey(ResourceDictionary dictionary, string key, string colorKey)
    {
        var brush = GetGradient(dictionary, key);
        brush.GradientStops.Should().NotBeEmpty();
        brush.GradientStops[0].Color.Should().Be(GetColor(dictionary, colorKey));
    }

    private static void AssertGradientHasOpaqueStops(ResourceDictionary dictionary, string key, int expectedStops)
    {
        var brush = GetGradient(dictionary, key);
        brush.GradientStops.Should().HaveCount(expectedStops);
        brush.GradientStops.Select(stop => stop.Color.A).Should().OnlyContain(alpha => alpha == 255);
    }

    private static void AssertShadowStructure(ResourceDictionary dictionary, string key)
    {
        var shadow = GetShadow(dictionary, key);
        shadow.RenderingBias.Should().Be(RenderingBias.Performance);
        shadow.BlurRadius.Should().BeGreaterThan(0);
        shadow.Opacity.Should().BeGreaterThan(0);
    }

    private static void AssertShadowRelativeIntensity(ResourceDictionary dictionary, string softerKey, string strongerKey)
    {
        var softer = GetShadow(dictionary, softerKey);
        var stronger = GetShadow(dictionary, strongerKey);

        softer.BlurRadius.Should().BeLessThan(stronger.BlurRadius, $"'{softerKey}' should stay lighter than '{strongerKey}'");
        softer.Opacity.Should().BeLessThan(stronger.Opacity, $"'{softerKey}' should stay lighter than '{strongerKey}'");
    }

    private static void AssertGlowShadowContrast(ResourceDictionary dictionary, string softerKey, string strongerKey)
    {
        var softer = GetShadow(dictionary, softerKey);
        var stronger = GetShadow(dictionary, strongerKey);

        softer.ShadowDepth.Should().Be(0);
        stronger.ShadowDepth.Should().Be(0);
        softer.BlurRadius.Should().BeGreaterThan(stronger.BlurRadius, $"'{softerKey}' should stay broader than '{strongerKey}'");
        softer.Opacity.Should().BeLessThan(stronger.Opacity, $"'{softerKey}' should stay more restrained than '{strongerKey}'");
    }

    private static void AssertShadowDepthZero(ResourceDictionary dictionary, string key)
    {
        GetShadow(dictionary, key).ShadowDepth.Should().Be(0, $"'{key}' should remain a glow-style shadow");
    }

    private static void AssertShadowMatchesColorKey(ResourceDictionary dictionary, string key, string colorKey)
    {
        GetShadow(dictionary, key).Color.Should().Be(GetColor(dictionary, colorKey), $"'{key}' should inherit from '{colorKey}'");
    }

    private static Color GetColor(ResourceDictionary dictionary, string key)
    {
        dictionary.Contains(key).Should().BeTrue($"theme color '{key}' should exist");
        return dictionary[key].Should().BeOfType<Color>().Subject;
    }

    private static SolidColorBrush GetBrush(ResourceDictionary dictionary, string key)
    {
        dictionary.Contains(key).Should().BeTrue($"theme brush '{key}' should exist");
        return dictionary[key].Should().BeOfType<SolidColorBrush>().Subject;
    }

    private static LinearGradientBrush GetGradient(ResourceDictionary dictionary, string key)
    {
        dictionary.Contains(key).Should().BeTrue($"theme gradient '{key}' should exist");
        return dictionary[key].Should().BeOfType<LinearGradientBrush>().Subject;
    }

    private static DropShadowEffect GetShadow(ResourceDictionary dictionary, string key)
    {
        dictionary.Contains(key).Should().BeTrue($"theme shadow '{key}' should exist");
        return dictionary[key].Should().BeOfType<DropShadowEffect>().Subject;
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
