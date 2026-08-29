using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FluentAssertions;
using WpfApplication = System.Windows.Application;

namespace ClassroomToolkit.Tests.UI;

/// <summary>
/// Contract for the LegacyAliases compatibility seam: every legacy resource key that feature XAML
/// still resolves must exist at application scope and map to the semantic CTK token, so theme
/// switching stays correct without migrating every feature file at once.
/// </summary>
[Collection("WPF UI")]
public sealed class LegacyAliasSeamTests
{
    private static readonly IReadOnlyDictionary<string, string> BrushToColorMappings = new Dictionary<string, string>
    {
        ["Brush_AppBackground"] = "CTK.Color.Canvas",
        ["Brush_Background"] = "CTK.Color.Canvas",
        ["Brush_Window_Atmosphere"] = "CTK.Color.Window",
        ["Brush_Surface_Primary"] = "CTK.Color.Surface",
        ["Brush_Background_L2"] = "CTK.Color.Surface",
        ["Brush_Surface_Secondary"] = "CTK.Color.SurfaceAlt",
        ["Brush_InputBackground"] = "CTK.Color.SurfaceElevated",
        ["Brush_Background_L3"] = "CTK.Color.SurfaceElevated",
        ["Brush_GlassSurface"] = "CTK.Color.OverlayToolbar",
        ["Brush_Glass_Surface"] = "CTK.Color.OverlayToolbar",
        ["Brush_OverlayMask"] = "CTK.Color.Overlay",
        ["Brush_Overlay_Dark"] = "CTK.Color.Overlay",
        ["Brush_Text_Primary"] = "CTK.Color.TextPrimary",
        ["Brush_Foreground"] = "CTK.Color.TextPrimary",
        ["Brush_Text_Secondary"] = "CTK.Color.TextSecondary",
        ["Brush_Text_Tertiary"] = "CTK.Color.TextTertiary",
        ["Brush_Text_Black"] = "CTK.Color.TextOnPrimary",
        ["Brush_Primary"] = "CTK.Color.Primary",
        ["Brush_Timer_Display"] = "CTK.Color.Primary",
        ["Brush_Timer_Neon"] = "CTK.Color.Primary",
        ["Brush_Border_Active"] = "CTK.Color.Primary",
        ["Brush_Primary_Hover"] = "CTK.Color.PrimaryHover",
        ["Brush_Primary_Light"] = "CTK.Color.PrimaryHover",
        ["Brush_Accent_Teal"] = "CTK.Color.PrimaryHover",
        ["Brush_Primary_Dark"] = "CTK.Color.PrimaryPressed",
        ["Brush_Teaching"] = "CTK.Color.Warning",
        ["Brush_Teaching_Deep"] = "CTK.Color.Warning",
        ["Brush_Warning"] = "CTK.Color.Warning",
        ["Brush_Accent_Amber"] = "CTK.Color.Warning",
        ["Brush_Success"] = "CTK.Color.Success",
        ["Brush_Danger"] = "CTK.Color.Danger",
        ["Brush_Pen_Red"] = "CTK.Color.Danger",
        ["Brush_Pen_Blue"] = "CTK.Color.Info",
        ["Brush_Accent_Violet"] = "CTK.Color.TextTertiary",
        ["Brush_Border_Subtle"] = "CTK.Color.BorderSubtle",
        ["Brush_Border"] = "CTK.Color.BorderSubtle",
        ["Brush_Border_Glass"] = "CTK.Color.BorderSubtle",
        ["Brush_Border_Strong"] = "CTK.Color.BorderDefault",
        ["Brush_Border_Light"] = "CTK.Color.BorderDefault",
        ["Brush_Glass_Border"] = "CTK.Color.BorderDefault",
        ["Brush_Border_Focus"] = "CTK.Color.BorderFocus",
        ["Brush_Surface_Hover"] = "CTK.Color.Hover",
        ["Brush_Surface_Active"] = "CTK.Color.Pressed",
        ["Brush_Surface_Tint"] = "CTK.Color.PrimarySoft",
        ["Brush_Danger_Surface"] = "CTK.Color.DangerSoft",
        ["Brush_Disabled"] = "CTK.Color.TextDisabled",
        ["Brush_White_Translucent"] = "CTK.Color.SelectionOverlay",
        ["Brush_White_Translucent_Low"] = "CTK.Color.SelectionOverlaySoft",
    };

    private static readonly string[] GradientKeys =
    [
        "Gradient_Primary_Subtle", "Gradient_Teaching_Subtle", "Gradient_Timer_Display",
        "Gradient_Hero_Glow", "Gradient_Shell_Surface", "Gradient_Card_Surface",
        "Gradient_Panel_Surface", "Gradient_Primary", "Gradient_Primary_Hover",
        "Gradient_Success", "Gradient_Danger", "Gradient_Warning",
        "Gradient_RollCall_Card", "Gradient_Launcher",
    ];

    private static readonly string[] ShadowKeys =
    [
        "Shadow_Card", "Shadow_Card_Subtle", "Shadow_Dialog", "Shadow_Dialog_Heavy",
        "Shadow_Floating", "Shadow_Popup_Light", "Shadow_Popup_Medium",
        "Shadow_Glow_Primary", "Shadow_Glow_Teaching", "Shadow_Glow_Hero",
        "Shadow_Primary_Glow", "Shadow_Danger_Glow", "Shadow_Glass_Glow",
    ];

    [Fact]
    public void LegacyBrushes_ShouldFollowSemanticTokens()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var resources = WpfApplication.Current.Resources;

            foreach (var (legacyKey, colorKey) in BrushToColorMappings)
            {
                var brush = resources[legacyKey].Should().BeOfType<SolidColorBrush>(
                    $"legacy '{legacyKey}' must stay a SolidColorBrush").Subject;
                brush.Color.Should().Be(
                    (Color)resources[colorKey],
                    $"legacy '{legacyKey}' must track semantic '{colorKey}' so theme switching stays correct");
            }
        });
    }

    [Fact]
    public void LegacyGradientsAndShadows_ShouldRemainAvailable()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var resources = WpfApplication.Current.Resources;

            foreach (var key in GradientKeys)
            {
                resources[key].Should().BeOfType<LinearGradientBrush>($"'{key}' must stay a LinearGradientBrush");
            }

            foreach (var key in ShadowKeys)
            {
                resources[key].Should().BeOfType<DropShadowEffect>($"'{key}' must stay a DropShadowEffect");
            }
        });
    }

    [Fact]
    public void LegacyStyleAliases_ShouldTargetSharedComponentStyles()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var resources = WpfApplication.Current.Resources;

            AssertStyle(resources, "Style_PrimaryButton", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_SecondaryButton", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_Button_Amber", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_Button_Teal", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_Button_Violet", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_DangerButton", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_IconButton", typeof(System.Windows.Controls.Button));
            AssertStyle(resources, "Style_IconButton_Active", typeof(System.Windows.Controls.Primitives.ToggleButton));
            AssertStyle(resources, "Style_ToggleButton", typeof(System.Windows.Controls.Primitives.ToggleButton));
        });
    }

    private static void AssertStyle(ResourceDictionary resources, string key, Type targetType)
    {
        var style = resources[key].Should().BeOfType<Style>($"legacy style '{key}' must exist").Subject;
        style.TargetType.Should().Be(targetType);
    }
}
