using FluentAssertions;
using System.Globalization;
using System.Xml.Linq;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSizeContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeShellSizeTokens()
    {
        var document = LoadWidgetStyles();

        var required = new[]
        {
            "Size_Shell_TitleBar_Dialog",
            "Size_Shell_TitleBar_Management",
            "Size_Shell_TitleBar_Work",
            "Spacing_Shell_CloseButton_Dialog",
            "Spacing_Shell_CloseButton_Management",
            "Spacing_Shell_TitleInset"
        };

        foreach (var token in required)
        {
            HasResourceKey(document, token).Should().BeTrue();
        }
    }

    [Fact]
    public void WidgetStyles_ShouldConsumeShellSizeTokens()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_DialogShellTitleBar", "Height", "Size_Shell_TitleBar_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellTitleBar", "Height", "Size_Shell_TitleBar_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_WorkShellTitleBar", "Height", "Size_Shell_TitleBar_Work").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Width", "Size_Button_Icon_Regular").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Height", "Size_Button_Icon_Regular").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Margin", "Spacing_Shell_CloseButton_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellCloseButton", "Margin", "Spacing_Shell_CloseButton_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellTitleText", "Margin", "Spacing_Shell_TitleInset").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellTitleText", "Margin", "Spacing_Shell_TitleInset").Should().BeTrue();
    }

    [Fact]
    public void WidgetStyles_ShouldKeepIconAndCloseHitAreaAboveMinimum()
    {
        var document = LoadWidgetStyles();

        GetDoubleResource(document, "Size_Button_Icon_Compact").Should().BeGreaterThanOrEqualTo(32d);
        GetDoubleResource(document, "Size_Button_Icon_Regular").Should().BeGreaterThanOrEqualTo(36d);
        GetDoubleResource(document, "Size_Button_Icon_Large").Should().BeGreaterThanOrEqualTo(40d);

        StyleUsesStaticResource(document, "Style_IconButton", "Width", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton", "Height", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton_Active", "Width", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_IconButton_Active", "Height", "Size_Button_Icon_Compact").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Width", "Size_Button_Icon_Regular").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellCloseButton", "Height", "Size_Button_Icon_Regular").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellCloseButton", "Width", "Size_Button_Icon_Large").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_OverlayShellCloseButton", "Height", "Size_Button_Icon_Large").Should().BeTrue();
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
            .FirstOrDefault(node => (string?)node.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == styleKey);
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
                string.Equals((string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")), key, StringComparison.Ordinal));

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
}
