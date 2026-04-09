using System;
using System.Windows;
using ClassroomToolkit.App.Helpers;
using WpfGrid = System.Windows.Controls.Grid;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void OnSceneCardsGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplySceneCardsLayout(e.NewSize.Width);
    }

    private void OnAdvancedModeCheckChanged(object sender, RoutedEventArgs e)
    {
        _advancedModeEnabled = AdvancedModeCheck?.IsChecked == true;
        UpdateAdvancedModeVisibility();
    }

    private void UpdateAdvancedModeVisibility()
    {
        var visibility = _advancedModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (PhotoAdvancedExpander != null)
        {
            PhotoAdvancedExpander.Visibility = visibility;
            if (!_advancedModeEnabled)
            {
                PhotoAdvancedExpander.IsExpanded = false;
            }
        }

        if (PresentationAdvancedExpander != null)
        {
            PresentationAdvancedExpander.Visibility = visibility;
            if (!_advancedModeEnabled)
            {
                PresentationAdvancedExpander.IsExpanded = false;
            }
        }
    }

    private void ApplySceneCardsLayout(double availableWidth)
    {
        if (PhotoPdfSettingsCard == null || WpsSettingsCard == null)
        {
            return;
        }

        var layoutMode = SceneCardsLayoutPolicy.Resolve(availableWidth);
        if (layoutMode == SceneCardsLayoutMode.SingleColumn)
        {
            WpfGrid.SetRow(PhotoPdfSettingsCard, 0);
            WpfGrid.SetColumn(PhotoPdfSettingsCard, 0);
            WpfGrid.SetColumnSpan(PhotoPdfSettingsCard, 2);
            PhotoPdfSettingsCard.Margin = new Thickness(0, 0, 0, 8);

            WpfGrid.SetRow(WpsSettingsCard, 1);
            WpfGrid.SetColumn(WpsSettingsCard, 0);
            WpfGrid.SetColumnSpan(WpsSettingsCard, 2);
            WpsSettingsCard.Margin = new Thickness(0, 8, 0, 0);
            return;
        }

        WpfGrid.SetRow(PhotoPdfSettingsCard, 0);
        WpfGrid.SetColumn(PhotoPdfSettingsCard, 0);
        WpfGrid.SetColumnSpan(PhotoPdfSettingsCard, 1);
        PhotoPdfSettingsCard.Margin = new Thickness(0, 0, 6, 0);

        WpfGrid.SetRow(WpsSettingsCard, 0);
        WpfGrid.SetColumn(WpsSettingsCard, 1);
        WpfGrid.SetColumnSpan(WpsSettingsCard, 1);
        WpsSettingsCard.Margin = new Thickness(6, 0, 0, 0);
    }

    private void UpdateBrushSizeLabel()
    {
        if (BrushSizeValue == null)
        {
            return;
        }

        BrushSizeValue.Text = $"{Math.Round(BrushSizeSlider.Value)}px";
    }

    private void UpdateBrushOpacityLabel()
    {
        if (BrushOpacityValue == null)
        {
            return;
        }

        BrushOpacityValue.Text = $"{Math.Round(BrushOpacitySlider.Value)}%";
    }

    private void UpdateEraserSizeLabel()
    {
        if (EraserSizeValue == null)
        {
            return;
        }

        EraserSizeValue.Text = $"{Math.Round(EraserSizeSlider.Value)}px";
    }

    private void UpdateCalligraphyOverlayThresholdLabel()
    {
        if (CalligraphyOverlayThresholdValue == null)
        {
            return;
        }

        CalligraphyOverlayThresholdValue.Text = $"{Math.Round(CalligraphyOverlayThresholdSlider.Value)}%";
    }

    private void UpdateCalligraphyOptionState()
    {
        bool isCalligraphy = ResolveBrushStyle() == PaintBrushStyle.Calligraphy;
        CalligraphyPresetCombo.Visibility = isCalligraphy ? Visibility.Visible : Visibility.Collapsed;
        WhiteboardPresetCombo.Visibility = isCalligraphy ? Visibility.Collapsed : Visibility.Visible;
        CalligraphyPresetCombo.IsEnabled = isCalligraphy;
        WhiteboardPresetCombo.IsEnabled = !isCalligraphy;
        if (CalligraphyAdvancedExpander != null)
        {
            CalligraphyAdvancedExpander.Visibility = isCalligraphy ? Visibility.Visible : Visibility.Collapsed;
            if (!isCalligraphy)
            {
                CalligraphyAdvancedExpander.IsExpanded = false;
            }
        }

        CalligraphyInkBloomCheck.IsEnabled = isCalligraphy;
        CalligraphySealCheck.IsEnabled = isCalligraphy;
        CalligraphyOverlayThresholdLabel.IsEnabled = isCalligraphy;
        CalligraphyOverlayThresholdSlider.IsEnabled = isCalligraphy;
        CalligraphyOverlayThresholdValue.IsEnabled = isCalligraphy;
    }

    private void UpdateClassroomWritingModeHint(ClassroomWritingMode mode)
    {
        if (ClassroomWritingModeHint == null)
        {
            return;
        }

        if (!ClassroomWritingModeHints.TryGetValue(mode, out var hint))
        {
            hint = ClassroomWritingModeHints[ClassroomWritingMode.Balanced];
        }

        ClassroomWritingModeHint.Text = hint;
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
