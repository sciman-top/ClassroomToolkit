using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetTypographyContractTests
{
    private static readonly XNamespace PresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void WidgetStyles_ShouldExposeTypographyTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertResourceValue(xaml, "FontSize_Body_S", "12");
        AssertResourceValue(xaml, "FontSize_Body_M", "13");
        AssertResourceValue(xaml, "FontSize_Title_Dialog", "14");
        AssertResourceValue(xaml, "FontSize_Title_Management", "15");
    }

    [Fact]
    public void ShellTypographyStyles_ShouldConsumeTypographyTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertStyleSetterResourceReference(xaml, "Style_ButtonFamilyBase", "FontSize", "FontSize_Body_M");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellTitleText", "FontSize", "FontSize_Title_Dialog");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellTitleText", "FontSize", "FontSize_Title_Management");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellSubtitleText", "FontSize", "FontSize_Body_S");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellFooterText", "FontSize", "FontSize_Body_S");
    }

    private static void AssertResourceValue(XDocument xaml, string key, string expectedValue)
    {
        var element = GetKeyedElement(xaml, key);
        element.Value.Trim().Should().Be(expectedValue);
    }

    private static void AssertStyleSetterResourceReference(XDocument xaml, string styleKey, string property, string expectedResourceKey)
    {
        var style = GetKeyedElement(xaml, styleKey);
        style.Name.LocalName.Should().Be("Style");

        var setter = style.Elements(PresentationNs + "Setter")
            .Single(element => (string?)element.Attribute("Property") == property);

        setter.Attribute("Value")!.Value.Should().Be($"{{StaticResource {expectedResourceKey}}}");
    }

    private static XElement GetKeyedElement(XDocument xaml, string key)
    {
        return xaml.Root!.Elements()
            .Single(element => (string?)element.Attribute(XamlNs + "Key") == key);
    }

    private static XDocument LoadWidgetStylesDocument()
    {
        return XDocument.Load(GetWidgetStylesPath());
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
}
