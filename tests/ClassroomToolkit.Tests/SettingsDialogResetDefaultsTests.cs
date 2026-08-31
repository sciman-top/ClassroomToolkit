using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.UI.Themes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

[Collection("WPF UI")]
public sealed class SettingsDialogResetDefaultsTests
{
    [Fact]
    public void PaintRestoreAll_ShouldRestorePersistedPaintColors()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new PaintSettingsDialog(new AppSettings
            {
                BrushColor = Colors.Green,
                BoardColor = Colors.Black,
                QuickColor1 = Colors.Purple,
                QuickColor2 = Colors.Orange,
                QuickColor3 = Colors.Brown
            });

            InvokeParameterless(dialog, "ApplyDefaultSettings");

            var defaults = new AppSettings();
            dialog.BrushColor.Should().Be(defaults.BrushColor);
            dialog.BoardColor.Should().Be(defaults.BoardColor);
            dialog.QuickColor1.Should().Be(defaults.QuickColor1);
            dialog.QuickColor2.Should().Be(defaults.QuickColor2);
            dialog.QuickColor3.Should().Be(defaults.QuickColor3);
            dialog.Close();
        });
    }

    [Fact]
    public void InkRestoreDefaults_ShouldRestoreAllVisibleSettings()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new InkSettingsDialog(new AppSettings
            {
                InkRecordEnabled = true,
                InkReplayPreviousEnabled = true,
                InkRetentionDays = 7,
                InkPhotoRootPath = @"D:\CustomInk"
            });

            InvokeParameterless(dialog, "ApplyDefaultSettings");

            var defaults = new AppSettings();
            Find<CheckBox>(dialog, "InkRecordCheck").IsChecked.Should().Be(defaults.InkRecordEnabled);
            Find<CheckBox>(dialog, "InkReplayPreviousCheck").IsChecked.Should().Be(defaults.InkReplayPreviousEnabled);
            Find<TextBox>(dialog, "InkRetentionDaysBox").Text.Should().Be(defaults.InkRetentionDays.ToString());
            Find<TextBox>(dialog, "InkPhotoPathBox").Text.Should().Be(defaults.InkPhotoRootPath);
            dialog.Close();
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    public void InkRetentionDays_ShouldRejectInvalidValues(string? value)
    {
        InkSettingsDialog.TryNormalizeRetentionDays(value, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("30", 30)]
    public void InkRetentionDays_ShouldAcceptNonNegativeIntegers(string value, int expected)
    {
        InkSettingsDialog.TryNormalizeRetentionDays(value, out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void AppearanceRestoreDefaults_ShouldSelectAndPublishDefaultTheme()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new AppearanceDialog(AppTheme.Light.ToString());
            AppTheme? selected = null;
            dialog.ThemeSelected += theme => selected = theme;

            Find<Button>(dialog, "RestoreDefaultsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Find<RadioButton>(dialog, "MidnightTealRadio").IsChecked.Should().BeTrue();
            selected.Should().Be(ThemePreferenceService.DefaultTheme);
            dialog.Close();
        });
    }

    private static T Find<T>(FrameworkElement root, string name)
        where T : FrameworkElement
    {
        return root.FindName(name).Should().BeAssignableTo<T>().Subject;
    }

    private static void InvokeParameterless(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.Invoke(target, null);
    }
}
