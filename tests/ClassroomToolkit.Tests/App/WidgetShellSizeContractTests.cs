using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSizeContractTests
{
    private static readonly XNamespace PresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void WidgetStyles_ShouldExposeShellSizeTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertResourceValue(xaml, "Size_Button_Icon_Compact", "32");
        AssertResourceValue(xaml, "Size_Button_Icon_Regular", "36");
        AssertResourceValue(xaml, "Size_Button_Icon_Large", "40");
        AssertResourceValue(xaml, "Size_Shell_TitleBar_Dialog", "44");
        AssertResourceValue(xaml, "Size_Shell_TitleBar_Management", "48");
        AssertResourceValue(xaml, "Size_Shell_TitleBar_Work", "40");
        AssertResourceValue(xaml, "Spacing_Shell_CloseButton_Dialog", "0,0,10,0");
        AssertResourceValue(xaml, "Spacing_Shell_CloseButton_Management", "0,0,12,0");
    }

    [Fact]
    public void WidgetStyles_ShouldConsumeShellSizeTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertStyleSetterResourceReference(xaml, "Style_IconButton", "Width", "Size_Button_Icon_Compact");
        AssertStyleSetterResourceReference(xaml, "Style_IconButton", "Height", "Size_Button_Icon_Compact");
        AssertStyleSetterResourceReference(xaml, "Style_IconButton_Active", "Width", "Size_Button_Icon_Compact");
        AssertStyleSetterResourceReference(xaml, "Style_IconButton_Active", "Height", "Size_Button_Icon_Compact");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellTitleBar", "Height", "Size_Shell_TitleBar_Dialog");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellTitleBar", "Height", "Size_Shell_TitleBar_Management");
        AssertStyleSetterResourceReference(xaml, "Style_WorkShellTitleBar", "Height", "Size_Shell_TitleBar_Work");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellCloseButton", "Width", "Size_Button_Icon_Regular");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellCloseButton", "Height", "Size_Button_Icon_Regular");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellCloseButton", "Margin", "Spacing_Shell_CloseButton_Management");
        AssertStyleSetterResourceReference(xaml, "Style_OverlayShellCloseButton", "Width", "Size_Button_Icon_Large");
        AssertStyleSetterResourceReference(xaml, "Style_OverlayShellCloseButton", "Height", "Size_Button_Icon_Large");
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
