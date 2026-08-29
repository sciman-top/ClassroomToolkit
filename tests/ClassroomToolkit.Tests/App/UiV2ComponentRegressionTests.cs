using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class UiV2ComponentRegressionTests
{
    private static readonly string[] SegmentedTabViews =
    [
        "src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml",
        "src/ClassroomToolkit.App/RollCallSettingsDialog.xaml",
        "src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml",
        "src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml"
    ];

    [Fact]
    public void FloatingToolbar_ShouldKeepItsMinimumHeightWithoutClippingTouchTargets()
    {
        var components = Load("src/ClassroomToolkit.App/UI/Styles/Components.xaml");
        var toolbar = FindStyle(components, "CTK.FloatingToolbar");

        toolbar.Elements().Should().Contain(setter =>
            IsSetter(setter, "MinHeight", "{StaticResource CTK.Size.Toolbar}"));
        toolbar.Elements().Should().NotContain(setter =>
            string.Equals((string?)setter.Attribute("Property"), "Height", StringComparison.Ordinal));
        toolbar.Elements().Should().Contain(setter => IsSetter(setter, "Padding", "5,4"));

        var toolbarXaml = Read("src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml");
        toolbarXaml.Should().Contain("Padding=\"5,4\"");
        toolbarXaml.Should().Contain("Padding=\"4,0\"");

        var toolbarCode = Read("src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs");
        toolbarCode.Should().Contain("Math.Ceiling(40.0 / scale)");
    }

    [Fact]
    public void SegmentedTabs_ShouldNotOverrideTheirTabControlItemContainerStyle()
    {
        foreach (var path in SegmentedTabViews)
        {
            Read(path).Should().NotContain("Style=\"{StaticResource CTK.TabItem}\"");
        }
    }

    [Fact]
    public void Sliders_ShouldRemainKeyboardFocusable()
    {
        var widgetStyles = Load("src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml");
        var implicitSliderStyle = widgetStyles.Descendants()
            .Single(element =>
                string.Equals(element.Name.LocalName, "Style", StringComparison.Ordinal) &&
                string.Equals((string?)element.Attribute("TargetType"), "Slider", StringComparison.Ordinal));
        implicitSliderStyle.Elements().Should().Contain(setter => IsSetter(setter, "Focusable", "True"));

        var components = Load("src/ClassroomToolkit.App/UI/Styles/Components.xaml");
        var slider = FindStyle(components, "CTK.Slider");
        slider.Elements().Should().Contain(setter =>
            IsSetter(setter, "FocusVisualStyle", "{StaticResource CTK.FocusVisual}"));
    }

    [Theory]
    [InlineData("Style_ManagementThumbnailListViewItem")]
    [InlineData("Style_ManagementFileListViewItem")]
    public void ImageManagerListItems_ShouldExposeKeyboardFocus(string styleKey)
    {
        var widgetStyles = Load("src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml");
        FindStyle(widgetStyles, styleKey).Descendants()
            .Should().Contain(trigger =>
                string.Equals(trigger.Name.LocalName, "Trigger", StringComparison.Ordinal) &&
                string.Equals((string?)trigger.Attribute("Property"), "IsKeyboardFocused", StringComparison.Ordinal));
    }

    [Fact]
    public void WarningResetContent_ShouldInheritTheButtonForeground()
    {
        var rollCall = Load("src/ClassroomToolkit.App/RollCallWindow.xaml");
        var resetButtons = rollCall.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "Button", StringComparison.Ordinal))
            .Where(element =>
                string.Equals((string?)element.Attribute("Click"), "OnResetClick", StringComparison.Ordinal) ||
                string.Equals((string?)element.Attribute("Click"), "OnTimerResetClick", StringComparison.Ordinal));

        resetButtons.Should().HaveCount(2);
        foreach (var resetButton in resetButtons)
        {
            resetButton.Descendants().Attributes().Should().NotContain(attribute =>
                attribute.Value.Contains("CTK.Brush.Text.OnPrimary", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void LightTheme_ShouldMeetTheRequiredContrastForSharedSemanticPairs()
    {
        var colors = ReadColors("src/ClassroomToolkit.App/UI/Themes/Colors.Light.xaml");

        Contrast(colors["CTK.Color.TextOnPrimary"], colors["CTK.Color.Primary"]).Should().BeGreaterThanOrEqualTo(4.5);
        Contrast(colors["CTK.Color.Warning"], colors["CTK.Color.WarningSoft"]).Should().BeGreaterThanOrEqualTo(4.5);
        Contrast(colors["CTK.Color.BorderDefault"], "#FFFFFF").Should().BeGreaterThanOrEqualTo(3.0);
    }

    private static XDocument Load(string relativePath) => XDocument.Load(TestPathHelper.ResolveRepoPath(relativePath.Split('/')));

    private static string Read(string relativePath) => File.ReadAllText(TestPathHelper.ResolveRepoPath(relativePath.Split('/')));

    private static XElement FindStyle(XDocument document, string key) => document.Descendants()
        .Single(element =>
            string.Equals(element.Name.LocalName, "Style", StringComparison.Ordinal) &&
            string.Equals((string?)element.Attribute(XamlKeyName), key, StringComparison.Ordinal));

    private static bool IsSetter(XElement element, string property, string value) =>
        string.Equals(element.Name.LocalName, "Setter", StringComparison.Ordinal) &&
        string.Equals((string?)element.Attribute("Property"), property, StringComparison.Ordinal) &&
        string.Equals((string?)element.Attribute("Value"), value, StringComparison.Ordinal);

    private static Dictionary<string, string> ReadColors(string relativePath) => Load(relativePath).Root!
        .Elements()
        .Where(element => string.Equals(element.Name.LocalName, "Color", StringComparison.Ordinal))
        .ToDictionary(
            element => (string)element.Attribute(XamlKeyName)!,
            element => element.Value.Trim(),
            StringComparer.Ordinal);

    private static double Contrast(string foreground, string background)
    {
        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(string value)
    {
        var hex = value.TrimStart('#');
        var offset = hex.Length == 8 ? 2 : 0;
        var red = Channel(hex, offset);
        var green = Channel(hex, offset + 2);
        var blue = Channel(hex, offset + 4);
        return 0.2126 * Linear(red) + 0.7152 * Linear(green) + 0.0722 * Linear(blue);
    }

    private static double Channel(string hex, int offset) =>
        int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

    private static double Linear(double value) => value <= 0.04045
        ? value / 12.92
        : Math.Pow((value + 0.055) / 1.055, 2.4);

    private static readonly XName XamlKeyName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
}
