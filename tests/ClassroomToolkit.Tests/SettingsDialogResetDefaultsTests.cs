using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
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

    [Fact]
    public void AutoExitRestoreDefault_ShouldSetFortyMinutes()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new AutoExitDialog(minutes: 5);

            Find<Button>(dialog, "RestoreDefaultButton")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Find<TextBox>(dialog, "MinutesBox").Text.Should().Be("40");
            dialog.Close();
        });
    }

    [Fact]
    public void PaintRestoreDefaults_ShouldRestoreVisibleSceneSettingsAndPreserveClassifierOverrides()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            const string overrides = "{\"office\":\"custom\"}";
            var dialog = new PaintSettingsDialog(new AppSettings
            {
                ControlMsPpt = false,
                ControlWpsPpt = false,
                InkCacheEnabled = false,
                PresentationClassifierOverridesJson = overrides
            });

            Find<CheckBox>(dialog, "ControlMsPptCheck").IsChecked.Should().BeFalse();
            Find<CheckBox>(dialog, "ControlWpsPptCheck").IsChecked.Should().BeFalse();
            Find<CheckBox>(dialog, "InkCacheCheck").IsChecked.Should().BeFalse();

            InvokeParameterless(dialog, "ApplyDefaultSettings");

            var defaults = new AppSettings();
            Find<CheckBox>(dialog, "ControlMsPptCheck").IsChecked.Should().Be(defaults.ControlMsPpt);
            Find<CheckBox>(dialog, "ControlWpsPptCheck").IsChecked.Should().Be(defaults.ControlWpsPpt);
            Find<CheckBox>(dialog, "InkCacheCheck").IsChecked.Should().Be(defaults.InkCacheEnabled);
            dialog.PresentationClassifierOverridesJson.Should().Contain("office");

            InvokeConfirmIgnoringDialogResult(dialog);

            dialog.ControlMsPpt.Should().Be(defaults.ControlMsPpt);
            dialog.ControlWpsPpt.Should().Be(defaults.ControlWpsPpt);
            dialog.InkCacheEnabled.Should().Be(defaults.InkCacheEnabled);
            dialog.PresentationClassifierOverridesJson.Should().Contain("office");
            dialog.Close();
        });
    }

    [Fact]
    public void PaintConfirm_ShouldClearClassifierOverridesOnlyWhenExplicitlyRequested()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new PaintSettingsDialog(new AppSettings
            {
                PresentationClassifierOverridesJson = "{\"wps\":\"custom\"}"
            });
            var clearOverrides = Find<CheckBox>(dialog, "PresentationClassifierClearOverridesCheck");
            clearOverrides.IsChecked = true;

            InvokeConfirmIgnoringDialogResult(dialog);

            dialog.PresentationClassifierOverridesJson.Should().BeEmpty();
            dialog.Close();
        });
    }

    [Fact]
    public void ImageManagerRestoreViewDefaults_ShouldRestoreViewAndShowInkPreference()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var window = new ImageManagerWindow(Array.Empty<string>(), Array.Empty<string>());
            window.ViewModel.ShowInkOverlay = false;
            var layoutDefaultsRaised = 0;
            var leftPanelChanged = 0;
            var showInkChanged = 0;
            window.LayoutDefaultsRequested += () => layoutDefaultsRaised++;
            window.LeftPanelLayoutChanged += (_, _) => leftPanelChanged++;
            window.ShowInkOverlayChanged += _ => showInkChanged++;

            Invoke(window, "OnRestoreLayoutDefaultsClick", window, new RoutedEventArgs(Button.ClickEvent));

            var defaults = new AppSettings();
            window.ViewModel.ShowInkOverlay.Should().Be(defaults.PhotoShowInkOverlay);
            window.ViewModel.ListMode.Should().Be(defaults.PhotoManagerListMode);
            Find<Slider>(window, "ThumbnailSizeSlider").Value.Should().Be(defaults.PhotoManagerThumbnailSize);
            layoutDefaultsRaised.Should().Be(1);
            leftPanelChanged.Should().Be(1);
            showInkChanged.Should().Be(1);
            window.Close();
        });
    }

    [Fact]
    public void ImageManagerRestorePhotoDefaults_ShouldRaiseTransformResetRequest()
    {
        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var window = new ImageManagerWindow(Array.Empty<string>(), Array.Empty<string>());
            var raised = 0;
            window.PhotoTransformDefaultsRequested += () => raised++;

            Invoke(window, "OnRestorePhotoTransformDefaultsClick", window, new RoutedEventArgs(Button.ClickEvent));

            raised.Should().Be(1);
            window.Close();
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

    private static void Invoke(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.Invoke(target, args);
    }

    private static void InvokeConfirmIgnoringDialogResult(PaintSettingsDialog dialog)
    {
        try
        {
            Invoke(dialog, "OnConfirm", dialog, new RoutedEventArgs(Button.ClickEvent));
        }
        catch (TargetInvocationException ex)
        {
            ex.InnerException.Should().BeOfType<InvalidOperationException>();
        }
    }
}
