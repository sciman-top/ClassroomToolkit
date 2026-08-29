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
        "CTK.Color.BorderToolbar",
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
    public void ThemeManager_ShouldRefreshSemanticAndLegacyBrushesForExistingDynamicResourceConsumers()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var application = (ClassroomToolkit.App.App)WpfApplication.Current;

            void AssertThemeSwitching()
            {
                var manager = new ThemeManager(application);
                var canvas = new System.Windows.Controls.Border();
                canvas.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "CTK.Brush.Canvas");

                manager.Apply(AppTheme.Blackboard).Should().BeTrue();
                manager.CurrentTheme.Should().Be(AppTheme.Blackboard);
                AssertActiveTheme(application!, canvas, Color.FromRgb(0x0F, 0x17, 0x14), Color.FromRgb(0x69, 0xC8, 0x9A));

                manager.Apply(AppTheme.Light).Should().BeTrue();
                manager.CurrentTheme.Should().Be(AppTheme.Light);
                AssertActiveTheme(application!, canvas, Color.FromRgb(0xF3, 0xF6, 0xF7), Color.FromRgb(0x0F, 0x76, 0x67));

                manager.Apply(AppTheme.MidnightTeal).Should().BeTrue();
                manager.CurrentTheme.Should().Be(AppTheme.MidnightTeal);
                AssertActiveTheme(application!, canvas, Color.FromRgb(0x0E, 0x14, 0x18), Color.FromRgb(0x35, 0xC7, 0xB0));
            }

            AssertThemeSwitching();
        });
    }

    [Fact]
    public void AppearanceDialog_ShouldApplyTheSelectedThemeToItsVisibleShell()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var application = (ClassroomToolkit.App.App)WpfApplication.Current;
            var manager = new ThemeManager(application);
            manager.Apply(AppTheme.MidnightTeal).Should().BeTrue();

            var dialog = new ClassroomToolkit.App.AppearanceDialog(AppTheme.MidnightTeal.ToString());
            dialog.ThemeSelected += theme => manager.Apply(theme);

            try
            {
                dialog.Show();
                application.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    static () => { });

                var shell = dialog.Content.Should().BeOfType<System.Windows.Controls.Border>().Subject;
                ((SolidColorBrush)shell.Background).Color.Should().Be(Color.FromRgb(0x11, 0x1A, 0x1F));

                var midnightCanvas = dialog.FindName("MidnightTealPreviewCanvas")
                    .Should().BeOfType<System.Windows.Controls.Border>().Subject;
                ((SolidColorBrush)midnightCanvas.Background).Color.Should().Be(Color.FromRgb(0x0E, 0x14, 0x18));
                var lightCanvas = dialog.FindName("LightPreviewCanvas")
                    .Should().BeOfType<System.Windows.Controls.Border>().Subject;
                ((SolidColorBrush)lightCanvas.Background).Color.Should().Be(Color.FromRgb(0xF3, 0xF6, 0xF7));

                var lightRadio = dialog.FindName("LightRadio")
                    .Should().BeOfType<System.Windows.Controls.RadioButton>().Subject;
                var blackboardRadio = dialog.FindName("BlackboardRadio")
                    .Should().BeOfType<System.Windows.Controls.RadioButton>().Subject;
                var midnightTealRadio = dialog.FindName("MidnightTealRadio")
                    .Should().BeOfType<System.Windows.Controls.RadioButton>().Subject;

                blackboardRadio.IsChecked = true;
                application.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    static () => { });
                manager.CurrentTheme.Should().Be(AppTheme.Blackboard);
                ((SolidColorBrush)shell.Background).Color.Should().Be(Color.FromRgb(0x13, 0x1E, 0x1A));

                lightRadio.IsChecked = true;
                application.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    static () => { });

                manager.CurrentTheme.Should().Be(AppTheme.Light);
                ((SolidColorBrush)application.Resources["CTK.Brush.Window"]).Color
                    .Should().Be(Color.FromRgb(0xFA, 0xFC, 0xFC));
                ((SolidColorBrush)shell.Background).Color.Should().Be(Color.FromRgb(0xFA, 0xFC, 0xFC));

                midnightTealRadio.IsChecked = true;
                application.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    static () => { });
                manager.CurrentTheme.Should().Be(AppTheme.MidnightTeal);
                ((SolidColorBrush)shell.Background).Color.Should().Be(Color.FromRgb(0x11, 0x1A, 0x1F));
            }
            finally
            {
                dialog.Close();
                manager.Apply(AppTheme.MidnightTeal).Should().BeTrue();
            }
        });
    }

    private static void AssertActiveTheme(
        WpfApplication application,
        System.Windows.Controls.Border canvas,
        Color expectedCanvas,
        Color expectedPrimary)
    {
        ((SolidColorBrush)application.Resources["CTK.Brush.Canvas"]).Color.Should().Be(expectedCanvas);
        ((SolidColorBrush)application.Resources["Brush_AppBackground"]).Color.Should().Be(expectedCanvas);
        ((SolidColorBrush)canvas.Background).Color.Should().Be(expectedCanvas);
        ((LinearGradientBrush)application.Resources["Gradient_Primary"]).GradientStops[0].Color.Should().Be(expectedPrimary);
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
