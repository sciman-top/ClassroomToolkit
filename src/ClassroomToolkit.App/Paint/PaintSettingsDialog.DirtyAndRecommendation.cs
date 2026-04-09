using System;
using System.Windows;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfTabItem = System.Windows.Controls.TabItem;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void UpdateSectionDirtyStates()
    {
        if (_suppressSectionDirtyTracking)
        {
            return;
        }

        SetTabHeader(SettingsTabControl, 0, "基础", IsPresetBrushSectionDirty());
        SetTabHeader(SettingsTabControl, 1, "工具栏", IsAdvancedSectionDirty());
        SetTabHeader(SettingsTabControl, 2, "兼容", IsSceneSectionDirty());
        UpdateChangeSummaryText();
    }

    private bool IsPresetBrushSectionDirty()
    {
        var current = CapturePresetBrushSectionStateFromControls();
        var initial = _initialPresetBrushSectionState;
        return !string.Equals(current.PresetScheme, initial.PresetScheme, StringComparison.OrdinalIgnoreCase)
            || current.BrushStyle != initial.BrushStyle
            || current.WhiteboardPreset != initial.WhiteboardPreset
            || current.CalligraphyPreset != initial.CalligraphyPreset
            || current.ClassroomWritingMode != initial.ClassroomWritingMode
            || current.BrushSizePx != initial.BrushSizePx
            || current.BrushOpacityPercent != initial.BrushOpacityPercent
            || current.EraserSizePx != initial.EraserSizePx
            || current.CalligraphyInkBloomEnabled != initial.CalligraphyInkBloomEnabled
            || current.CalligraphySealEnabled != initial.CalligraphySealEnabled
            || current.CalligraphyOverlayThresholdPercent != initial.CalligraphyOverlayThresholdPercent
            || !string.Equals(current.WpsInputMode, initial.WpsInputMode, StringComparison.OrdinalIgnoreCase)
            || current.WpsWheelForward != initial.WpsWheelForward
            || current.LockStrategyWhenDegraded != initial.LockStrategyWhenDegraded
            || current.AutoFallbackFailureThreshold != initial.AutoFallbackFailureThreshold
            || current.AutoFallbackProbeIntervalCommands != initial.AutoFallbackProbeIntervalCommands
            || current.WpsDebounceMs != initial.WpsDebounceMs
            || current.PhotoPostInputRefreshDelayMs != initial.PhotoPostInputRefreshDelayMs
            || Math.Abs(current.PhotoWheelZoomBase - initial.PhotoWheelZoomBase) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || Math.Abs(current.PhotoGestureZoomSensitivity - initial.PhotoGestureZoomSensitivity) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || !string.Equals(current.PhotoInertiaProfile, initial.PhotoInertiaProfile, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSceneSectionDirty()
    {
        var current = CaptureSceneSectionStateFromControls();
        var initial = _initialSceneSectionState;
        return current.InkSaveEnabled != initial.InkSaveEnabled
            || current.InkExportScope != initial.InkExportScope
            || current.InkExportMaxParallelFiles != initial.InkExportMaxParallelFiles
            || current.PhotoCrossPageDisplay != initial.PhotoCrossPageDisplay
            || current.PhotoRememberTransform != initial.PhotoRememberTransform
            || current.PhotoInputTelemetryEnabled != initial.PhotoInputTelemetryEnabled
            || current.PhotoNeighborPrefetchRadiusMax != initial.PhotoNeighborPrefetchRadiusMax
            || current.PhotoPostInputRefreshDelayMs != initial.PhotoPostInputRefreshDelayMs
            || Math.Abs(current.PhotoWheelZoomBase - initial.PhotoWheelZoomBase) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || Math.Abs(current.PhotoGestureZoomSensitivity - initial.PhotoGestureZoomSensitivity) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || !string.Equals(current.PhotoInertiaProfile, initial.PhotoInertiaProfile, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.OfficeInputMode, initial.OfficeInputMode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.WpsInputMode, initial.WpsInputMode, StringComparison.OrdinalIgnoreCase)
            || current.WpsWheelForward != initial.WpsWheelForward
            || current.ForcePresentationForegroundOnFullscreen != initial.ForcePresentationForegroundOnFullscreen
            || current.WpsDebounceMs != initial.WpsDebounceMs
            || current.LockStrategyWhenDegraded != initial.LockStrategyWhenDegraded
            || current.PresentationAutoFallbackFailureThreshold != initial.PresentationAutoFallbackFailureThreshold
            || current.PresentationAutoFallbackProbeIntervalCommands != initial.PresentationAutoFallbackProbeIntervalCommands
            || current.PresentationClassifierAutoLearnEnabled != initial.PresentationClassifierAutoLearnEnabled
            || current.PresentationClassifierClearOverridesRequested != initial.PresentationClassifierClearOverridesRequested
            || !string.Equals(
                NormalizePresentationClassifierOverridesJson(current.PresentationClassifierOverridesJson),
                NormalizePresentationClassifierOverridesJson(initial.PresentationClassifierOverridesJson),
                StringComparison.Ordinal);
    }

    private bool IsAdvancedSectionDirty()
    {
        var current = CaptureAdvancedSectionStateFromControls();
        var initial = _initialAdvancedSectionState;
        return current.ShapeType != initial.ShapeType
            || Math.Abs(current.ToolbarScale - initial.ToolbarScale) > PaintSettingsDefaults.ComboTagComparisonEpsilon;
    }

    private static void SetTabHeader(WpfTabControl? tabs, int index, string baseHeader, bool isDirty)
    {
        if (tabs == null || index < 0 || index >= tabs.Items.Count)
        {
            return;
        }

        if (tabs.Items[index] is not WpfTabItem tabItem)
        {
            return;
        }

        tabItem.Header = isDirty ? $"{baseHeader} *" : baseHeader;
    }

    private void UpdatePresetRecommendation(string currentPreset)
    {
        if (PresetSchemeRecommendationText == null)
        {
            return;
        }

        var recommendedScheme = _presetRecommendation.Scheme;
        var isRecommendedValid = PresetSchemePolicy.TryResolveManagedParameters(recommendedScheme, out _);
        if (!isRecommendedValid)
        {
            PresetSchemeRecommendationText.Text = string.Empty;
            PresetSchemeRecommendationText.Visibility = Visibility.Collapsed;
            return;
        }

        var recommendedLabel = ResolvePresetDisplayName(recommendedScheme);
        bool alreadyApplied = string.Equals(currentPreset, recommendedScheme, StringComparison.OrdinalIgnoreCase);
        var prefix = alreadyApplied
            ? $"设备画像推荐：{recommendedLabel}（已应用）。"
            : $"设备画像推荐：{recommendedLabel}。";
        var recommendation = $"{prefix}{_presetRecommendation.Reason}";
        PresetSchemeRecommendationText.Text = recommendation;
        PresetSchemeRecommendationText.Visibility = string.IsNullOrWhiteSpace(recommendation)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string ResolvePresetDisplayName(string scheme)
    {
        foreach (var (label, value) in PresetSchemeChoices)
        {
            if (string.Equals(value, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }
        }

        return "课堂平衡（推荐）";
    }
}
