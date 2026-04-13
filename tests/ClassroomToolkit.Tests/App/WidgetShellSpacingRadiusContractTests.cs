using FluentAssertions;
using System.Xml.Linq;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSpacingRadiusContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeShellSpacingAndRadiusTokens()
    {
        var document = LoadWidgetStyles();

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
            "Spacing_Control_ManagementFooter",
            "Radius_Shell_Dialog",
            "Radius_Shell_Management",
            "Radius_Shell_Work",
            "Radius_Shell_WorkBottomBar",
            "Radius_Shell_OverlaySideRail",
            "Radius_Shell_OverlayHintBadge",
            "Radius_Control_Panel",
            "Spacing_Control_WorkShellBottomBar",
            "Spacing_Control_BubbleShellRoot",
            "Spacing_Control_BubbleShellItem",
            "Spacing_Control_OverlayHintBadge",
            "Radius_Control_BubbleShellRoot",
            "Radius_Control_BubbleShellItem"
        };

        foreach (var token in requiredTokens)
        {
            HasResourceKey(document, token).Should().BeTrue();
        }
    }

    [Fact]
    public void WidgetStyles_ShellStyles_ShouldConsumeSpacingAndRadiusTokens()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_DialogShellWindowBorder", "CornerRadius", "Radius_Shell_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellWindowBorder", "Margin", "Spacing_Shell_DialogMargin").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellTitleText", "Margin", "Spacing_Shell_TitleInset").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellActionBar", "Margin", "Spacing_Shell_ActionBar").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellSecondaryActionButton", "Margin", "Spacing_Shell_ActionGap").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellWindowBorder", "CornerRadius", "Radius_Shell_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellContentBorder", "Margin", "Spacing_Shell_ManagementContent").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellFooterBorder", "Padding", "Spacing_Control_ManagementFooter").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellWindowBorder", "CornerRadius", "Radius_Shell_Work").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellTitleBar", "Margin", "Spacing_Shell_WorkTitleBar").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellContentHost", "Margin", "Spacing_Shell_WorkContent").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellBottomBar", "CornerRadius", "Radius_Shell_WorkBottomBar").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellBottomBar", "Padding", "Spacing_Control_WorkShellBottomBar").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellBottomBar", "Margin", "Spacing_Shell_WorkBottomBar").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellSideRail", "CornerRadius", "Radius_Shell_OverlaySideRail").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellHintBadge", "CornerRadius", "Radius_Shell_OverlayHintBadge").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellHintBadge", "Padding", "Spacing_Control_OverlayHintBadge").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_BubbleShellRoot", "CornerRadius", "Radius_Control_BubbleShellRoot").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_BubbleShellRoot", "Padding", "Spacing_Control_BubbleShellRoot").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_BubbleShellItem", "CornerRadius", "Radius_Control_BubbleShellItem").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_BubbleShellItem", "Padding", "Spacing_Control_BubbleShellItem").Should().BeTrue();
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

    private static XDocument LoadWidgetStyles() => XDocument.Load(GetWidgetStylesPath());

    private static bool HasResourceKey(XDocument document, string key)
    {
        return document
            .Descendants()
            .Any(node => string.Equals((string?)node.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")), key, StringComparison.Ordinal));
    }

    private static bool StyleUsesStaticResource(XDocument document, string styleKey, string propertyName, string resourceKey)
    {
        var style = document
            .Descendants()
            .Where(node => string.Equals(node.Name.LocalName, "Style", StringComparison.Ordinal))
            .FirstOrDefault(node => string.Equals((string?)node.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")), styleKey, StringComparison.Ordinal));
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
}
