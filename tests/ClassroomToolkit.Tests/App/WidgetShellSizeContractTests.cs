using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSizeContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeShellSizeTokens()
    {
        var document = LoadWidgetStyles();

        AssertResourceValue(document, "Size_Button_Icon_Compact", "32");
        AssertResourceValue(document, "Size_Button_Icon_Regular", "36");
        AssertResourceValue(document, "Size_Button_Icon_Large", "40");
        AssertResourceValue(document, "Size_Shell_TitleBar_Dialog", "44");
        AssertResourceValue(document, "Size_Shell_TitleBar_Management", "48");
        AssertResourceValue(document, "Size_Shell_TitleBar_Work", "40");
        AssertResourceValue(document, "Spacing_Shell_CloseButton_Dialog", "0,0,10,0");
        AssertResourceValue(document, "Spacing_Shell_CloseButton_Management", "0,0,12,0");
        HasResourceKey(document, "Spacing_Shell_TitleInset").Should().BeTrue();
    }

    [Fact]
    public void WidgetStyles_ShouldConsumeShellSizeTokens()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_IconButton", "Width", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton", "Height", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton_Active", "Width", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton_Active", "Height", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellTitleBar", "Height", "Size_Shell_TitleBar_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellTitleBar", "Height", "Size_Shell_TitleBar_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellTitleBar", "Height", "Size_Shell_TitleBar_Work").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Width", "Size_Button_Icon_Regular").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Height", "Size_Button_Icon_Regular").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Margin", "Spacing_Shell_CloseButton_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellCloseButton", "Margin", "Spacing_Shell_CloseButton_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellTitleText", "Margin", "Spacing_Shell_TitleInset").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellTitleText", "Margin", "Spacing_Shell_TitleInset").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellCloseButton", "Width", "Size_Button_Icon_Large").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellCloseButton", "Height", "Size_Button_Icon_Large").Should().BeTrue();
    }

    [Fact]
    public void WidgetStyles_ShouldKeepIconAndCloseHitAreaAboveMinimum()
    {
        var document = LoadWidgetStyles();

        GetDoubleResource(document, "Size_Button_Icon_Compact").Should().BeGreaterThanOrEqualTo(32d);
        GetDoubleResource(document, "Size_Button_Icon_Regular").Should().BeGreaterThanOrEqualTo(36d);
        GetDoubleResource(document, "Size_Button_Icon_Large").Should().BeGreaterThanOrEqualTo(40d);
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

    private static double GetDoubleResource(XDocument document, string key)
    {
        var node = document
            .Descendants()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "Double", StringComparison.Ordinal) &&
                string.Equals((string?)element.Attribute(XamlKeyName), key, StringComparison.Ordinal));

        node.Should().NotBeNull($"resource '{key}' should exist");
        return double.Parse(node!.Value, CultureInfo.InvariantCulture);
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
