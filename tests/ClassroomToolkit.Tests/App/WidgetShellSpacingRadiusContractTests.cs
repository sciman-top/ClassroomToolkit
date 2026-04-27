using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSpacingRadiusContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeShellSpacingAndRadiusTokens()
    {
        var document = LoadWidgetStyles();

        AssertResourceValue(document, "Spacing_Shell_DialogMargin", "10");
        AssertResourceValue(document, "Spacing_Shell_TitleInset", "18,0,0,0");
        AssertResourceValue(document, "Spacing_Shell_ActionBar", "18,0,18,18");
        AssertResourceValue(document, "Spacing_Shell_ActionGap", "0,0,8,0");
        AssertResourceValue(document, "Spacing_Shell_WorkTitleBar", "18,0,14,0");
        AssertResourceValue(document, "Spacing_Shell_WorkContent", "18,2,18,8");
        AssertResourceValue(document, "Spacing_Shell_WorkBottomBar", "18,0,18,14");
        AssertResourceValue(document, "Spacing_Shell_ManagementContent", "12,8,12,8");
        AssertResourceValue(document, "Radius_Shell_Dialog", "12");
        AssertResourceValue(document, "Radius_Shell_Management", "12");
        AssertResourceValue(document, "Radius_Shell_Work", "14");
        AssertResourceValue(document, "Radius_Shell_WorkBottomBar", "10");
        AssertResourceValue(document, "Radius_Shell_OverlaySideRail", "20");
        AssertResourceValue(document, "Radius_Shell_OverlayHintBadge", "10");

        var additionalTokens = new[]
        {
            "Spacing_Control_ManagementFooter",
            "Radius_Control_Panel",
            "Spacing_Control_WorkShellBottomBar",
            "Spacing_Control_BubbleShellRoot",
            "Spacing_Control_BubbleShellItem",
            "Spacing_Control_OverlayHintBadge",
            "Radius_Control_BubbleShellRoot",
            "Radius_Control_BubbleShellItem"
        };

        foreach (var token in additionalTokens)
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
        StyleUsesStaticResource(document, "Style_DialogShellTitleBar", "Height", "Size_Shell_TitleBar_Dialog").Should().BeTrue();
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

    private static XDocument LoadWidgetStyles() => XDocument.Load(TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Assets",
        "Styles",
        "WidgetStyles.xaml"));

    private static void AssertResourceValue(XDocument document, string key, string expectedValue)
    {
        GetKeyedElement(document, key).Value.Trim().Should().Be(expectedValue);
    }

    private static XElement GetKeyedElement(XDocument document, string key)
    {
        return document.Root!.Elements()
            .Single(element => string.Equals((string?)element.Attribute(XamlKeyName), key, StringComparison.Ordinal));
    }

    private static bool HasResourceKey(XDocument document, string key)
    {
        return document
            .Descendants()
            .Any(node => string.Equals((string?)node.Attribute(XamlKeyName), key, StringComparison.Ordinal));
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
