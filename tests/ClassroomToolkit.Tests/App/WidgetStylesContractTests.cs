using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetStylesContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeStageAShellStyleKeys()
    {
        var document = LoadWidgetStyles();

        var requiredKeys = new[]
        {
            "Style_ButtonFamilyBase",
            "Style_PrimaryButton",
            "Style_SecondaryButton",
            "Style_DangerButton",
            "Style_ToggleButton",
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
            "Style_DialogShellTitleText",
            "Style_ManagementShellWindowBorder",
            "Style_ManagementShellTitleBar",
            "Style_ManagementShellTitleText",
            "Style_WorkShellWindowBorder",
            "Style_WorkShellTitleBar",
            "Style_OverlayShellCloseButton",
            "Style_OverlayShellSideRail",
            "Style_OverlayShellHintBadge",
            "Style_BubbleShellRoot",
            "Style_BubbleShellItem",
            "Style_BubbleShellSelectedItem",
            "Style_FullscreenShellSideRail",
            "Style_FullscreenShellHintBadge",
            "Style_ListViewItem_SelectionUnified",
            "Style_ListBoxItem_SelectionUnified",
            "Style_CleanListBox"
        };

        foreach (var key in requiredKeys)
        {
            GetKeyedElement(document, key).Name.LocalName.Should().Be("Style");
        }
    }

    [Fact]
    public void WidgetStyles_ShouldReferenceStageASemanticTokens()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_ButtonFamilyBase", "FontSize", "FontSize_Body_M").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellTitleText", "FontSize", "FontSize_Title_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellTitleText", "FontSize", "FontSize_Title_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellSubtitleText", "FontSize", "FontSize_Body_S").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellFooterText", "FontSize", "FontSize_Body_S").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellWindowBorder", "Background", "Brush_AppBackground").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellWindowBorder", "BorderBrush", "Brush_Border_Subtle").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellSideRail", "Background", "Brush_OverlayMask").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellHintBadge", "Background", "Brush_OverlayMask").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellHintBadge", "BorderBrush", "Brush_Border_Strong").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ButtonFamilyBase", "Padding", "Spacing_Control_ButtonFamily").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_SecondaryButton", "Padding", "Spacing_Control_ButtonFamily_Compact").Should().BeTrue();
        TargetTypeStyleUsesStaticResource(document, "MenuItem", "Padding", "Spacing_Control_MenuItem").Should().BeTrue();
    }

    [Fact]
    public void WidgetStyles_ShouldApplyControlSpacingTokensInControlStyles()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_ButtonFamilyBase", "Padding", "Spacing_Control_ButtonFamily").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_SecondaryButton", "Padding", "Spacing_Control_ButtonFamily_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ListViewItem_SelectionUnified", "Padding", "Spacing_Control_ListItem").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ListBoxItem_SelectionUnified", "Padding", "Spacing_Control_ListItem").Should().BeTrue();
        TargetTypeStyleUsesStaticResource(document, "TreeViewItem", "Padding", "Spacing_Control_TreeItem").Should().BeTrue();
        TargetTypeStyleUsesStaticResource(document, "GridViewColumnHeader", "Padding", "Spacing_Control_Header").Should().BeTrue();
        TargetTypeStyleUsesStaticResource(document, "TabItem", "Padding", "Spacing_Control_TabItem").Should().BeTrue();
    }

    private static XDocument LoadWidgetStyles() => XDocument.Load(TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Assets",
        "Styles",
        "WidgetStyles.xaml"));

    private static XElement GetKeyedElement(XDocument document, string key)
    {
        return document.Root!.Elements()
            .Single(element => string.Equals((string?)element.Attribute(XamlKeyName), key, StringComparison.Ordinal));
    }

    private static bool StyleUsesStaticResource(XDocument document, string styleKey, string propertyName, string resourceKey)
    {
        var style = document
            .Descendants()
            .Where(node => string.Equals(node.Name.LocalName, "Style", StringComparison.Ordinal))
            .FirstOrDefault(node => string.Equals((string?)node.Attribute(XamlKeyName), styleKey, StringComparison.Ordinal));
        if (style is null)
        {
            return false;
        }

        return style
            .Elements()
            .Where(node => string.Equals(node.Name.LocalName, "Setter", StringComparison.Ordinal))
            .Any(setter =>
                string.Equals((string?)setter.Attribute("Property"), propertyName, StringComparison.Ordinal) &&
                string.Equals(GetStaticResourceKey(setter), resourceKey, StringComparison.Ordinal));
    }

    private static bool TargetTypeStyleUsesStaticResource(XDocument document, string targetType, string propertyName, string resourceKey)
    {
        var styles = document
            .Descendants()
            .Where(node => string.Equals(node.Name.LocalName, "Style", StringComparison.Ordinal))
            .Where(node => string.Equals((string?)node.Attribute("TargetType"), targetType, StringComparison.Ordinal) ||
                           string.Equals((string?)node.Attribute("TargetType"), "{x:Type " + targetType + "}", StringComparison.Ordinal));

        return styles
            .SelectMany(style => style.Elements().Where(node => string.Equals(node.Name.LocalName, "Setter", StringComparison.Ordinal)))
            .Any(setter =>
                string.Equals((string?)setter.Attribute("Property"), propertyName, StringComparison.Ordinal) &&
                string.Equals(GetStaticResourceKey(setter), resourceKey, StringComparison.Ordinal));
    }

    private static string? GetStaticResourceKey(XElement setter)
    {
        var inlineValue = (string?)setter.Attribute("Value");
        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            var trimmed = inlineValue.Trim();
            const string prefix = "{StaticResource ";
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                return trimmed.Substring(prefix.Length, trimmed.Length - prefix.Length - 1).Trim();
            }
        }

        var nestedStaticResource = setter
            .Descendants()
            .FirstOrDefault(node => string.Equals(node.Name.LocalName, "StaticResource", StringComparison.Ordinal));

        return (string?)nestedStaticResource?.Attribute("ResourceKey");
    }

    private static readonly XName XamlKeyName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
}
