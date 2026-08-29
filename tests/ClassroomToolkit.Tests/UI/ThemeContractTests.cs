using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.UI.Themes;
using FluentAssertions;
using Xunit;
using WpfApplication = System.Windows.Application;

namespace ClassroomToolkit.Tests.UI;

[Collection("WPF UI")]
public sealed class ThemeContractTests
{
    private static readonly string[] RequiredColorKeys =
    [
        "CTK.Color.Canvas",
        "CTK.Color.Window",
        "CTK.Color.Surface",
        "CTK.Color.SurfaceAlt",
        "CTK.Color.SurfaceElevated",
        "CTK.Color.Hover",
        "CTK.Color.Pressed",
        "CTK.Color.BorderSubtle",
        "CTK.Color.BorderDefault",
        "CTK.Color.BorderFocus",
        "CTK.Color.TextPrimary",
        "CTK.Color.TextSecondary",
        "CTK.Color.TextTertiary",
        "CTK.Color.TextDisabled",
        "CTK.Color.TextOnPrimary",
        "CTK.Color.Primary",
        "CTK.Color.PrimaryHover",
        "CTK.Color.PrimaryPressed",
        "CTK.Color.PrimarySoft",
        "CTK.Color.Info",
        "CTK.Color.InfoSoft",
        "CTK.Color.Success",
        "CTK.Color.SuccessSoft",
        "CTK.Color.Warning",
        "CTK.Color.WarningSoft",
        "CTK.Color.Danger",
        "CTK.Color.DangerPressed",
        "CTK.Color.DangerSoft",
        "CTK.Color.Selection",
        "CTK.Color.SelectionOverlay",
        "CTK.Color.SelectionOverlaySoft",
        "CTK.Color.Shadow",
        "CTK.Color.Overlay",
        "CTK.Color.OverlayToolbar"
    ];

    [Theory]
    [InlineData(null, AppTheme.MidnightTeal)]
    [InlineData("", AppTheme.MidnightTeal)]
    [InlineData("  blackboard ", AppTheme.Blackboard)]
    [InlineData("LIGHT", AppTheme.Light)]
    [InlineData("not-a-theme", AppTheme.MidnightTeal)]
    [InlineData("999", AppTheme.MidnightTeal)]
    public void ThemePreferenceService_ShouldFailClosed(string? raw, AppTheme expected)
    {
        ThemePreferenceService.Parse(raw).Should().Be(expected);
        ThemePreferenceService.Normalize(raw).Should().Be(expected.ToString());
    }

    [Fact]
    public void AppSettings_ShouldDefaultToMidnightTeal()
    {
        new AppSettings().UiTheme.Should().Be(AppTheme.MidnightTeal.ToString());
    }

    [Theory]
    [InlineData("MidnightTeal")]
    [InlineData("Blackboard")]
    [InlineData("Light")]
    public void ColorDictionary_ShouldExposeTheSameSemanticContract(string theme)
    {
        WpfStaTestRunner.Run(() =>
        {
            var dictionary = LoadColorDictionary(theme);

            foreach (var key in RequiredColorKeys)
            {
                dictionary[key].Should().BeOfType<Color>(key);
            }
        });
    }

    [Fact]
    public void ThemeManager_ShouldReplaceOnlyNestedColorDictionary()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var application = (ClassroomToolkit.App.App)WpfApplication.Current;

            void AssertThemeSwitching()
            {
                var themeResources = application!.Resources.MergedDictionaries
                    .Single(dictionary => dictionary.Source?.OriginalString.Contains("ThemeResources.xaml", StringComparison.OrdinalIgnoreCase) == true);
                var semanticBrushes = themeResources.MergedDictionaries[1];

                var manager = new ThemeManager(application);

                manager.Apply(AppTheme.Blackboard).Should().BeTrue();
                manager.CurrentTheme.Should().Be(AppTheme.Blackboard);
                themeResources.MergedDictionaries.Should().HaveCount(2);
                themeResources.MergedDictionaries[1].Should().BeSameAs(semanticBrushes);
                ((Color)themeResources.MergedDictionaries[0]["CTK.Color.Primary"])
                    .Should().Be(Color.FromRgb(0x69, 0xC8, 0x9A));

                manager.Apply(AppTheme.Light).Should().BeTrue();
                manager.CurrentTheme.Should().Be(AppTheme.Light);
                themeResources.MergedDictionaries.Should().HaveCount(2);
                themeResources.MergedDictionaries[1].Should().BeSameAs(semanticBrushes);
                ((Color)themeResources.MergedDictionaries[0]["CTK.Color.Primary"])
                    .Should().Be(Color.FromRgb(0x0F, 0x76, 0x67));
            }

            AssertThemeSwitching();
        });
    }

    private static ResourceDictionary LoadColorDictionary(string theme)
    {
        return (ResourceDictionary)WpfApplication.LoadComponent(
            new Uri(ComponentUri($"UI/Themes/Colors.{theme}.xaml"), UriKind.Relative));
    }

    private static string ComponentUri(string resourcePath)
    {
        var assemblyName = Uri.EscapeDataString(typeof(AppSettings).Assembly.GetName().Name ?? "ClassroomToolkit.App");
        return $"/{assemblyName};component/{resourcePath}";
    }
}
