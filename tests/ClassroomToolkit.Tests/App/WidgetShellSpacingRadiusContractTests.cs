using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetShellSpacingRadiusContractTests
{
    private static readonly XNamespace PresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void WidgetStyles_ShouldExposeShellSpacingAndRadiusTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertResourceValue(xaml, "Spacing_Shell_DialogMargin", "10");
        AssertResourceValue(xaml, "Spacing_Shell_TitleInset", "18,0,0,0");
        AssertResourceValue(xaml, "Spacing_Shell_ActionBar", "20,0,20,20");
        AssertResourceValue(xaml, "Spacing_Shell_ActionGap", "0,0,8,0");
        AssertResourceValue(xaml, "Spacing_Shell_WorkTitleBar", "18,0,14,0");
        AssertResourceValue(xaml, "Spacing_Shell_WorkContent", "18,2,18,8");
        AssertResourceValue(xaml, "Spacing_Shell_WorkBottomBar", "18,0,18,14");
        AssertResourceValue(xaml, "Spacing_Shell_ManagementContent", "12,8,12,8");
        AssertResourceValue(xaml, "Radius_Shell_Dialog", "15");
        AssertResourceValue(xaml, "Radius_Shell_Management", "14");
        AssertResourceValue(xaml, "Radius_Shell_Work", "18");
        AssertResourceValue(xaml, "Radius_Shell_WorkBottomBar", "14");
        AssertResourceValue(xaml, "Radius_Shell_OverlaySideRail", "20");
        AssertResourceValue(xaml, "Radius_Shell_OverlayHintBadge", "10");
    }

    [Fact]
    public void WidgetStyles_ShellStyles_ShouldConsumeSpacingAndRadiusTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertStyleSetterResourceReference(xaml, "Style_DialogShellWindowBorder", "CornerRadius", "Radius_Shell_Dialog");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellWindowBorder", "Margin", "Spacing_Shell_DialogMargin");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellTitleBar", "Height", "Size_Shell_TitleBar_Dialog");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellTitleText", "Margin", "Spacing_Shell_TitleInset");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellActionBar", "Margin", "Spacing_Shell_ActionBar");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellSecondaryActionButton", "Margin", "Spacing_Shell_ActionGap");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellWindowBorder", "CornerRadius", "Radius_Shell_Management");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellContentBorder", "Margin", "Spacing_Shell_ManagementContent");
        AssertStyleSetterResourceReference(xaml, "Style_WorkShellWindowBorder", "CornerRadius", "Radius_Shell_Work");
        AssertStyleSetterResourceReference(xaml, "Style_WorkShellTitleBar", "Margin", "Spacing_Shell_WorkTitleBar");
        AssertStyleSetterResourceReference(xaml, "Style_WorkShellContentHost", "Margin", "Spacing_Shell_WorkContent");
        AssertStyleSetterResourceReference(xaml, "Style_WorkShellBottomBar", "CornerRadius", "Radius_Shell_WorkBottomBar");
        AssertStyleSetterResourceReference(xaml, "Style_WorkShellBottomBar", "Margin", "Spacing_Shell_WorkBottomBar");
        AssertStyleSetterResourceReference(xaml, "Style_OverlayShellSideRail", "CornerRadius", "Radius_Shell_OverlaySideRail");
        AssertStyleSetterResourceReference(xaml, "Style_OverlayShellHintBadge", "CornerRadius", "Radius_Shell_OverlayHintBadge");
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
