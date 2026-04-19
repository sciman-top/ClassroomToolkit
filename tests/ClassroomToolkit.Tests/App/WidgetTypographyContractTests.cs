using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetTypographyContractTests
{
    [Fact]
    public void WidgetStyles_ShouldExposeTypographyTokens()
    {
        var document = LoadWidgetStyles();

        AssertResourceValue(document, "FontSize_Body_S", "12");
        AssertResourceValue(document, "FontSize_Body_M", "13");
        AssertResourceValue(document, "FontSize_Title_Dialog", "14");
        AssertResourceValue(document, "FontSize_Title_Management", "15");
        HasResourceKey(document, "FontSize_Control_Label").Should().BeTrue();
        HasResourceKey(document, "FontSize_Control_Helper").Should().BeTrue();
    }

    [Fact]
    public void ShellTypographyStyles_ShouldConsumeTypographyTokens()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_ButtonFamilyBase", "FontSize", "FontSize_Body_M").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ToggleButton", "FontSize", "FontSize_Control_Label").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_DialogShellTitleText", "FontSize", "FontSize_Title_Dialog").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellTitleText", "FontSize", "FontSize_Title_Management").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellSubtitleText", "FontSize", "FontSize_Body_S").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellFooterText", "FontSize", "FontSize_Body_S").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ListViewItem_SelectionUnified", "FontSize", "FontSize_Control_Label").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ListBoxItem_SelectionUnified", "FontSize", "FontSize_Control_Label").Should().BeTrue();
    }

    [Fact]
    public void ControlHelperTypography_ShouldBeUsedByHelperTextStyles()
    {
        var document = LoadWidgetStyles();

        StyleUsesStaticResource(document, "Style_ManagementShellSubtitleText", "FontSize", "FontSize_Body_S").Should().BeTrue();
        StyleUsesStaticResource(document, "Style_ManagementShellFooterText", "FontSize", "FontSize_Body_S").Should().BeTrue();
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
