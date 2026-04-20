using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class TouchFirstMetricsXamlContractTests
{
    [Fact]
    public void WidgetStyles_ShouldDefineTouchTargetTokens()
    {
        var document = LoadWidgetStyles();

        AssertResourceValue(document, "Size_Touch_Target_Min", "44");
        AssertResourceValue(document, "Size_Touch_Target_Primary", "48");
        AssertResourceValue(document, "Size_Touch_Target_Bubble_Hit", "56");
        AssertResourceValue(document, "Size_Touch_ScrollBar", "16");
    }

    [Fact]
    public void WidgetStyles_ShouldApplyTouchTargetTokensToSharedCompactControls()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_IconButton", "MinWidth", "Size_Touch_Target_Min").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton", "MinHeight", "Size_Touch_Target_Min").Should().BeTrue();
        StyleTemplateCentersVisualBorder(document, "Style_IconButton").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton_Active", "MinWidth", "Size_Touch_Target_Primary").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton_Active", "MinHeight", "Size_Touch_Target_Primary").Should().BeTrue();
        StyleTemplateCentersVisualBorder(document, "Style_IconButton_Active").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ColorBubbleToggle", "MinWidth", "Size_Touch_Target_Bubble_Hit").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ColorBubbleToggle", "MinHeight", "Size_Touch_Target_Bubble_Hit").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ColorPaletteButton", "MinWidth", "Size_Touch_Target_Min").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ColorPaletteButton", "MinHeight", "Size_Touch_Target_Min").Should().BeTrue();
        TargetTypeStyleUsesStaticResource(document, "ScrollBar", "Width", "Size_Touch_ScrollBar").Should().BeTrue();
        TargetTypeStyleUsesStaticResource(document, "ScrollBar", "MinWidth", "Size_Touch_ScrollBar").Should().BeTrue();
        TargetTypeStyleHasSetter(document, "ScrollBar", "Height").Should().BeFalse();
        TargetTypeStyleHasSetter(document, "ScrollBar", "MinHeight").Should().BeFalse();
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

    private static bool StyleUsesStaticResource(XDocument document, string styleKey, string propertyName, string resourceKey)
    {
        var style = FindStyleByKey(document, styleKey);
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

    private static bool StyleTemplateCentersVisualBorder(XDocument document, string styleKey)
    {
        var style = FindStyleByKey(document, styleKey);
        var border = style?
            .Descendants()
            .FirstOrDefault(node =>
                string.Equals(node.Name.LocalName, "Border", StringComparison.Ordinal) &&
                string.Equals((string?)node.Attribute(XamlNameName), "border", StringComparison.Ordinal));

        if (border is null)
        {
            return false;
        }

        return string.Equals((string?)border.Attribute("Width"), "{TemplateBinding Width}", StringComparison.Ordinal) &&
               string.Equals((string?)border.Attribute("Height"), "{TemplateBinding Height}", StringComparison.Ordinal) &&
               string.Equals((string?)border.Attribute("HorizontalAlignment"), "Center", StringComparison.Ordinal) &&
               string.Equals((string?)border.Attribute("VerticalAlignment"), "Center", StringComparison.Ordinal);
    }

    private static XElement? FindStyleByKey(XDocument document, string styleKey)
    {
        return document
            .Descendants()
            .Where(node => string.Equals(node.Name.LocalName, "Style", StringComparison.Ordinal))
            .FirstOrDefault(node => string.Equals((string?)node.Attribute(XamlKeyName), styleKey, StringComparison.Ordinal));
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

    private static bool TargetTypeStyleHasSetter(XDocument document, string targetType, string propertyName)
    {
        var styles = document
            .Descendants()
            .Where(node => string.Equals(node.Name.LocalName, "Style", StringComparison.Ordinal))
            .Where(node => string.Equals((string?)node.Attribute("TargetType"), targetType, StringComparison.Ordinal) ||
                           string.Equals((string?)node.Attribute("TargetType"), "{x:Type " + targetType + "}", StringComparison.Ordinal));

        return styles
            .SelectMany(style => style.Elements().Where(node => string.Equals(node.Name.LocalName, "Setter", StringComparison.Ordinal)))
            .Any(setter => string.Equals((string?)setter.Attribute("Property"), propertyName, StringComparison.Ordinal));
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
    private static readonly XName XamlNameName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
}
