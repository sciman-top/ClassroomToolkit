using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WidgetStylesContractTests
{
    private static readonly XNamespace PresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void WidgetStyles_ShouldExposeStageAShellStyleKeys()
    {
        var xaml = LoadWidgetStylesDocument();

        var requiredKeys = new[]
        {
            "Style_PrimaryButton",
            "Style_SecondaryButton",
            "Style_DangerButton",
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
            "Style_ManagementShellWindowBorder",
            "Style_WorkShellWindowBorder",
            "Style_OverlayShellCloseButton",
            "Style_OverlayShellSideRail",
            "Style_OverlayShellHintBadge",
            "Style_BubbleShellRoot",
            "Style_BubbleShellItem",
            "Style_BubbleShellSelectedItem",
            "Style_FullscreenShellSideRail",
            "Style_FullscreenShellHintBadge"
        };

        foreach (var key in requiredKeys)
        {
            GetKeyedElement(xaml, key).Name.LocalName.Should().Be("Style");
        }
    }

    [Fact]
    public void WidgetStyles_ShouldReferenceStageASemanticTokens()
    {
        var xaml = LoadWidgetStylesDocument();

        AssertStyleSetterResourceReference(xaml, "Style_ButtonFamilyBase", "FontSize", "FontSize_Body_M");
        AssertStyleSetterResourceReference(xaml, "Style_DialogShellTitleText", "FontSize", "FontSize_Title_Dialog");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellTitleText", "FontSize", "FontSize_Title_Management");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellSubtitleText", "FontSize", "FontSize_Body_S");
        AssertStyleSetterResourceReference(xaml, "Style_ManagementShellFooterText", "FontSize", "FontSize_Body_S");
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
