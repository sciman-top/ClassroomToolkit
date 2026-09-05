using ClassroomToolkit.App;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

// 全部“稳定化默认值”常量镜像断言集中在此：调整默认值时同步更新对应分组，
// 防止调优常量在重构中被无意改动。
public sealed class StabilizedDefaultsContractTests
{
    [Fact]
    public void MainWindowRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        MainWindowRuntimeDefaults.OverlayActivationRetouchMinIntervalMs.Should().Be(100);
        MainWindowRuntimeDefaults.ExplicitForegroundRetouchMinIntervalMs.Should().Be(120);
        MainWindowRuntimeDefaults.StartupDiagnosticsDialogDelayMs.Should().Be(1800);
        MainWindowRuntimeDefaults.LauncherMinutesToSeconds.Should().Be(60);
        MainWindowRuntimeDefaults.NumericComparisonEpsilon.Should().Be(0.0001);
    }

    [Fact]
    public void PhotoSelectionPreparationDefaults_ShouldMatchStabilizedValues()
    {
        PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs.Should().Be(800);
    }

    [Fact]
    public void RollCallRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        RollCallRuntimeDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
        RollCallRuntimeDefaults.ClassSwitchSuppressMs.Should().Be(250);
    }

    [Fact]
    public void WindowDedupDefaults_ShouldMatchStabilizedValues()
    {
        WindowDedupDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void WindowInteropRetryDefaults_ShouldMatchStabilizedValues()
    {
        WindowInteropRetryDefaults.MaxRetryAttempts.Should().Be(2);
        WindowInteropRetryDefaults.ErrorInvalidWindowHandle.Should().Be(1400);
        WindowInteropRetryDefaults.ErrorInvalidHandle.Should().Be(6);
    }

    [Fact]
    public void WindowInteropRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        WindowInteropRuntimeDefaults.RetrySleepMs.Should().Be(0);
    }

    [Fact]
    public void BrushPredictionPreviewDefaults_ShouldMatchStabilizedValues()
    {
        BrushPredictionPreviewDefaults.MinPredictionDtSeconds.Should().Be(1e-6);
        BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor.Should().Be(0.68);
        BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor.Should().Be(0.32);
        BrushPredictionPreviewDefaults.MinSpeedDipPerSec.Should().Be(12.0);
        BrushPredictionPreviewDefaults.DampingSpeedReference.Should().Be(2600.0);
        BrushPredictionPreviewDefaults.DampingMin.Should().Be(0.72);
        BrushPredictionPreviewDefaults.FirstLeadHorizonRatio.Should().Be(0.45);
        BrushPredictionPreviewDefaults.SecondLeadHorizonRatio.Should().Be(0.95);
        BrushPredictionPreviewDefaults.FirstLeadDistanceRatio.Should().Be(0.7);
        BrushPredictionPreviewDefaults.SpeedFactorRange.Should().Be(620.0);
        BrushPredictionPreviewDefaults.BaseWidthFactor.Should().Be(0.17);
        BrushPredictionPreviewDefaults.SpeedWidthGainFactor.Should().Be(0.09);
        BrushPredictionPreviewDefaults.MinBaseWidthDip.Should().Be(0.95);
        BrushPredictionPreviewDefaults.MidWidthRatio.Should().Be(0.82);
        BrushPredictionPreviewDefaults.TipWidthRatio.Should().Be(0.68);
        BrushPredictionPreviewDefaults.MinMidWidthDip.Should().Be(0.8);
        BrushPredictionPreviewDefaults.MinTipWidthDip.Should().Be(0.7);
        BrushPredictionPreviewDefaults.InitialBaseWidthFactor.Should().Be(0.2);
        BrushPredictionPreviewDefaults.InitialBaseWidthMinDip.Should().Be(0.9);
        BrushPredictionPreviewDefaults.InitialTipWidthRatio.Should().Be(0.78);
        BrushPredictionPreviewDefaults.PrimaryAlphaMultiplier.Should().Be(0.34);
        BrushPredictionPreviewDefaults.SecondaryAlphaMultiplier.Should().Be(0.24);
        BrushPredictionPreviewDefaults.TipAlphaMultiplier.Should().Be(0.18);
        BrushPredictionPreviewDefaults.TipRadiusRatio.Should().Be(0.5);
    }

    [Fact]
    public void CalligraphyRenderingDefaults_ShouldMatchStabilizedValues()
    {
        CalligraphyRenderingDefaults.SealStrokeWidthFactor.Should().Be(0.08);
        CalligraphyRenderingDefaults.DegradeAreaThreshold.Should().Be(160000.0);
        CalligraphyRenderingDefaults.DegradeLayerThreshold.Should().Be(22);
        CalligraphyRenderingDefaults.MaxRibbonLayersNormal.Should().Be(18);
        CalligraphyRenderingDefaults.MaxRibbonLayersDegraded.Should().Be(8);
        CalligraphyRenderingDefaults.MaxBloomLayersNormal.Should().Be(10);
        CalligraphyRenderingDefaults.MaxBloomLayersDegraded.Should().Be(4);
        CalligraphyRenderingDefaults.AdaptiveLevelMax.Should().Be(2);
        CalligraphyRenderingDefaults.AdaptiveHighCostMs.Should().Be(6.4);
        CalligraphyRenderingDefaults.AdaptiveLowCostMs.Should().Be(3.8);
        CalligraphyRenderingDefaults.AdaptiveCostEmaAlpha.Should().Be(0.2);
        CalligraphyRenderingDefaults.AdaptiveAreaThresholdStep.Should().Be(25000);
        CalligraphyRenderingDefaults.AdaptiveLayerThresholdStep.Should().Be(4);
    }

    [Fact]
    public void CrossPageBoundsCacheDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageBoundsCacheDefaults.InteractiveReuseMaxAgeMs.Should().Be(120);
        CrossPageBoundsCacheDefaults.KeyEpsilon.Should().Be(0.01);
    }

    [Fact]
    public void CrossPageBrushContinuationBridgeDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageBrushContinuationBridgeDefaults.DeltaYEpsilon.Should().Be(0.01);
        CrossPageBrushContinuationBridgeDefaults.InterpolationLowerExclusive.Should().Be(0.0);
        CrossPageBrushContinuationBridgeDefaults.InterpolationUpperExclusive.Should().Be(1.0);
        CrossPageBrushContinuationBridgeDefaults.SeedTimestampIncrementTicks.Should().Be(1);
    }

    [Fact]
    public void CrossPageBrushContinuationDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageBrushContinuationDefaults.InPageOffsetDipMin.Should().Be(0.08);
        CrossPageBrushContinuationDefaults.InPageOffsetDipMax.Should().Be(0.22);
        CrossPageBrushContinuationDefaults.InPageOffsetFactor.Should().Be(0.01);
        CrossPageBrushContinuationDefaults.ReplayDistanceToleranceDip.Should().Be(0.35);
    }

    [Fact]
    public void CrossPageDisplayUpdateThrottleDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs.Should().Be(0);
        CrossPageDisplayUpdateThrottleDefaults.MinDelayedDispatchMs.Should().Be(1);
    }

    [Fact]
    public void CrossPageInkVisualSyncDedupDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs.Should().Be(64);
    }

    [Fact]
    public void CrossPageInputSwitchDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageInputSwitchDefaults.MinPositiveHysteresisDip.Should().Be(0);
    }

    [Fact]
    public void CrossPageInteractiveHoldDurationDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageInteractiveHoldDurationDefaults.BaseMs.Should().Be(220);
        CrossPageInteractiveHoldDurationDefaults.ExtraPerNeighborMs.Should().Be(40);
        CrossPageInteractiveHoldDurationDefaults.MaxMs.Should().Be(380);
        CrossPageInteractiveHoldDurationDefaults.BrushModeExtraMs.Should().Be(80);
        CrossPageInteractiveHoldDurationDefaults.EraserModeExtraMs.Should().Be(40);
    }

    [Fact]
    public void CrossPageInteractiveSwitchClampDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageInteractiveSwitchClampDefaults.MinPageIndex.Should().Be(1);
        CrossPageInteractiveSwitchClampDefaults.MinFallbackPageHeight.Should().Be(1.0);
        CrossPageInteractiveSwitchClampDefaults.MinResolvedPageHeight.Should().Be(0.0);
    }

    [Fact]
    public void CrossPageMissingNeighborRefreshNormalizationDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs.Should().Be(1);
        CrossPageMissingNeighborRefreshNormalizationDefaults.MinMissingThreshold.Should().Be(1);
    }

    [Fact]
    public void CrossPageNeighborPagesClearDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageNeighborPagesClearDefaults.MinGraceMs.Should().Be(0);
    }

    [Fact]
    public void CrossPageNeighborPrefetchDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageNeighborPrefetchDefaults.RadiusDefault.Should().Be(2);
        CrossPageNeighborPrefetchDefaults.RadiusMin.Should().Be(1);
        CrossPageNeighborPrefetchDefaults.RadiusMax.Should().Be(4);
        CrossPageNeighborPrefetchDefaults.NeighborInkCacheLimit.Should().Be(10);
    }

    [Fact]
    public void CrossPageRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageRuntimeDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void CrossPageViewportBoundsDefaults_ShouldMatchStabilizedValues()
    {
        CrossPageViewportBoundsDefaults.VisibilityMarginDip.Should().Be(16.0);
        CrossPageViewportBoundsDefaults.CenterRatio.Should().Be(0.5);
        CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip.Should().Be(0.5);
        CrossPageViewportBoundsDefaults.ClampSlackMinDip.Should().Be(32.0);
        CrossPageViewportBoundsDefaults.ClampSlackViewportRatio.Should().Be(0.5);
    }

    [Fact]
    public void InkCacheRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        InkCacheRuntimeDefaults.HistoryLimit.Should().Be(20);
        InkCacheRuntimeDefaults.MaxHistoryMemoryBytes.Should().Be(512L * 1024L * 1024L);
        InkCacheRuntimeDefaults.NoiseTileCacheLimit.Should().Be(96);
        InkCacheRuntimeDefaults.SolidBrushCacheLimit.Should().Be(256);
        InkCacheRuntimeDefaults.PenCacheLimit.Should().Be(192);
    }

    [Fact]
    public void InkGeometryDefaults_ShouldMatchStabilizedValues()
    {
        InkGeometryDefaults.MinSelectionRectSideDip.Should().Be(1.0);
        InkGeometryDefaults.MinShapeStrokeThicknessDip.Should().Be(1.0);
        InkGeometryDefaults.MinShapeRectSideDip.Should().Be(1.0);
        InkGeometryDefaults.MinPenThicknessDip.Should().Be(1.0);
        InkGeometryDefaults.MinEraserRadiusDip.Should().Be(2.0);
        InkGeometryDefaults.EraserMoveThresholdMinDip.Should().Be(1.0);
        InkGeometryDefaults.EraserMoveThresholdScale.Should().Be(0.2);
        InkGeometryDefaults.EraserTapDistanceThresholdDip.Should().Be(0.5);
    }

    [Fact]
    public void InkInputRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        InkInputRuntimeDefaults.PredictionUpdateMinDtMs.Should().Be(0.5);
        InkInputRuntimeDefaults.RegionSelectionStrokeThicknessDip.Should().Be(2.0);
        InkInputRuntimeDefaults.RegionEraseMinSideDip.Should().Be(2.0);
        InkInputRuntimeDefaults.PhotoReferenceSizeMinDip.Should().Be(0.5);
    }

    [Fact]
    public void InkPredictionDefaults_ShouldMatchStabilizedValues()
    {
        InkPredictionDefaults.HorizonMinMs.Should().Be(4);
        InkPredictionDefaults.HorizonMaxMs.Should().Be(16);
        InkPredictionDefaults.MaxDistanceDip.Should().Be(10.0);
        InkPredictionDefaults.PrimaryAlphaMin.Should().Be(24);
        InkPredictionDefaults.PrimaryAlphaMax.Should().Be(136);
        InkPredictionDefaults.SecondaryAlphaMin.Should().Be(18);
        InkPredictionDefaults.SecondaryAlphaMax.Should().Be(110);
        InkPredictionDefaults.TipAlphaMin.Should().Be(14);
        InkPredictionDefaults.TipAlphaMax.Should().Be(92);
    }

    [Fact]
    public void InkRenderBatchingDefaults_ShouldMatchStabilizedValues()
    {
        InkRenderBatchingDefaults.ProximityPaddingPixels.Should().Be(24);
        InkRenderBatchingDefaults.AreaRatioThreshold.Should().Be(1.6);
    }

    [Fact]
    public void InkRenderingCacheDefaults_ShouldMatchStabilizedValues()
    {
        InkRenderingCacheDefaults.PenWidthMinMilli.Should().Be(1);
        InkRenderingCacheDefaults.PenWidthQuantizeScale.Should().Be(1000.0);
    }

    [Fact]
    public void InkRuntimeTimingDefaults_ShouldMatchStabilizedValues()
    {
        InkRuntimeTimingDefaults.CalligraphyPreviewMinIntervalMs.Should().Be(16);
        InkRuntimeTimingDefaults.InputCooldownMs.Should().Be(120);
        InkRuntimeTimingDefaults.MonitorActiveIntervalMs.Should().Be(600);
        InkRuntimeTimingDefaults.MonitorIdleIntervalMs.Should().Be(1400);
        InkRuntimeTimingDefaults.IdleThresholdMs.Should().Be(2500);
        InkRuntimeTimingDefaults.RedrawMinIntervalMs.Should().Be(16);
        InkRuntimeTimingDefaults.PhotoPanRedrawThresholdDip.Should().Be(3);
        InkRuntimeTimingDefaults.RedrawDispatchDelayMinMs.Should().Be(1);
        InkRuntimeTimingDefaults.SidecarAutoSaveDelayMs.Should().Be(600);
        InkRuntimeTimingDefaults.SidecarAutoSaveRetryMax.Should().Be(3);
        InkRuntimeTimingDefaults.SidecarAutoSaveRetryDelayMs.Should().Be(900);
        InkRuntimeTimingDefaults.CalligraphyAdaptiveAdjustMinIntervalMs.Should().Be(200);
        InkRuntimeTimingDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void InputGeometryDefaults_ShouldMatchStabilizedValues()
    {
        InputGeometryDefaults.MinRenderableImageSideDip.Should().Be(0.5);
    }

    [Fact]
    public void OverlayInputPassthroughDefaults_ShouldMatchStabilizedValues()
    {
        OverlayInputPassthroughDefaults.OpacityEpsilon.Should().Be(0.001);
    }

    [Fact]
    public void PaintPresetDefaults_ShouldMatchStabilizedValues()
    {
        PaintPresetDefaults.WpsDebounceDefaultMs.Should().Be(120);
        PaintPresetDefaults.WpsDebounceLegacyDefaultMs.Should().Be(200);
        PaintPresetDefaults.PostInputRefreshDefaultMs.Should().Be(120);
        PaintPresetDefaults.WpsDebounceBalancedMs.Should().Be(120);
        PaintPresetDefaults.WpsDebounceResponsiveMs.Should().Be(80);
        PaintPresetDefaults.WpsDebounceStableMs.Should().Be(200);
        PaintPresetDefaults.WpsDebounceDualScreenMs.Should().Be(160);
        PaintPresetDefaults.PostInputBalancedMs.Should().Be(120);
        PaintPresetDefaults.PostInputResponsiveMs.Should().Be(80);
        PaintPresetDefaults.PostInputStableMs.Should().Be(140);
        PaintPresetDefaults.PostInputDualScreenMs.Should().Be(160);
        PaintPresetDefaults.WheelZoomBalanced.Should().Be(1.0008);
        PaintPresetDefaults.WheelZoomResponsive.Should().Be(1.0010);
        PaintPresetDefaults.WheelZoomStable.Should().Be(1.0006);
        PaintPresetDefaults.WheelZoomDualScreen.Should().Be(1.0007);
        PaintPresetDefaults.GestureSensitivityResponsive.Should().Be(1.2);
        PaintPresetDefaults.GestureSensitivityStable.Should().Be(0.8);
        PaintPresetDefaults.GestureSensitivityDualScreen.Should().Be(0.9);
    }

    [Fact]
    public void PaintSettingsDefaults_ShouldMatchStabilizedValues()
    {
        PaintSettingsDefaults.DoubleComparisonEpsilon.Should().Be(0.0001);
        PaintSettingsDefaults.ComboTagComparisonEpsilon.Should().Be(0.001);
        PaintSettingsDefaults.PercentMin.Should().Be(0.0);
        PaintSettingsDefaults.PercentMax.Should().Be(100.0);
        PaintSettingsDefaults.PercentToByteScale.Should().Be(255.0);
    }

    [Fact]
    public void PaintSettingsOptionDefaults_ShouldMatchStabilizedValues()
    {
        PaintSettingsOptionDefaults.InkExportMaxParallelDefault.Should().Be(2);
        PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault.Should().Be(4);
    }

    [Fact]
    public void PhotoDocumentRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        PhotoDocumentRuntimeDefaults.PdfDefaultDpi.Should().Be(96);
        PhotoDocumentRuntimeDefaults.PdfCacheLimit.Should().Be(6);
        PhotoDocumentRuntimeDefaults.PdfCacheMaxBytes.Should().Be(100L * 1024L * 1024L);
        PhotoDocumentRuntimeDefaults.PdfCacheTryEnterTimeoutMs.Should().Be(50);
        PhotoDocumentRuntimeDefaults.PdfPrefetchTryEnterTimeoutMs.Should().Be(100);
        PhotoDocumentRuntimeDefaults.PdfPrefetchDelayMs.Should().Be(120);
        PhotoDocumentRuntimeDefaults.NeighborPageCacheLimit.Should().Be(5);
    }

    [Fact]
    public void PhotoHorizontalPanRangeDefaults_ShouldMatchStabilizedValues()
    {
        PhotoHorizontalPanRangeDefaults.MinSlackDip.Should().Be(24.0);
        PhotoHorizontalPanRangeDefaults.SlackRatio.Should().Be(0.06);
    }

    [Fact]
    public void PhotoInertiaProfileDefaults_ShouldMatchStabilizedValues()
    {
        PhotoInertiaProfileDefaults.Normalize("SENSITIVE").Should().Be(PhotoInertiaProfileDefaults.Sensitive);
        PhotoInertiaProfileDefaults.Normalize(" heavy ").Should().Be(PhotoInertiaProfileDefaults.Heavy);
        PhotoInertiaProfileDefaults.Normalize("legacy").Should().Be(PhotoInertiaProfileDefaults.Standard);
        PhotoInertiaProfileDefaults.Normalize(null).Should().Be(PhotoInertiaProfileDefaults.Standard);
    }

    [Fact]
    public void PhotoInputAlignmentDefaults_ShouldMatchStabilizedValues()
    {
        PhotoInputAlignmentDefaults.GestureSensitivityMin.Should().Be(0.2);
        PhotoInputAlignmentDefaults.GestureSensitivityMax.Should().Be(3.0);
        PhotoInputAlignmentDefaults.MinEventFactorFloor.Should().Be(0.01);
        PhotoInputAlignmentDefaults.IgnoreFactorDelta.Should().Be(0.001);
        PhotoInputAlignmentDefaults.PanResistanceFactorDefault.Should().Be(0.42);
        PhotoInputAlignmentDefaults.PanResistanceFactorMin.Should().Be(0.05);
        PhotoInputAlignmentDefaults.PanResistanceFactorMax.Should().Be(0.95);
    }

    [Fact]
    public void PhotoInputConflictDefaults_ShouldMatchStabilizedValues()
    {
        PhotoInputConflictDefaults.SuppressWindowMinMs.Should().Be(0);
        PhotoInputConflictDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void PhotoPanInertiaDefaults_ShouldMatchStabilizedValues()
    {
        PhotoPanInertiaDefaults.MouseTickIntervalMs.Should().Be(16);
        PhotoPanInertiaDefaults.MouseDecelerationDipPerMs2.Should().Be(0.0022);
        PhotoPanInertiaDefaults.MouseStopSpeedDipPerMs.Should().Be(0.012);
        PhotoPanInertiaDefaults.MouseFrameElapsedMinMs.Should().Be(1.0);
        PhotoPanInertiaDefaults.MouseFrameElapsedMaxMs.Should().Be(34.0);
        PhotoPanInertiaDefaults.MouseMaxDurationMs.Should().Be(1100.0);
        PhotoPanInertiaDefaults.MouseMaxTranslationPerFrameDip.Should().Be(150.0);
        PhotoPanInertiaDefaults.MouseMinReleaseSpeedDipPerMs.Should().Be(0.06);
        PhotoPanInertiaDefaults.MouseMaxReleaseSpeedDipPerMs.Should().Be(4.4);
        PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip.Should().Be(0.9);
        PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs.Should().Be(140);
        PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs.Should().Be(6);
        PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs.Should().Be(120);
        PhotoPanInertiaDefaults.MouseVelocitySampleHistoryMaxAgeMs.Should().Be(220);
        PhotoPanInertiaDefaults.MouseVelocitySampleCapacity.Should().Be(12);
        PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain.Should().Be(0.75);
        PhotoPanInertiaDefaults.TouchMinVelocitySampleDistanceDip.Should().Be(0.55);
        PhotoPanInertiaDefaults.TouchMaxVelocitySampleAgeMs.Should().Be(220);
        PhotoPanInertiaDefaults.TouchVelocitySampleWindowMs.Should().Be(170);
        PhotoPanInertiaDefaults.TouchVelocityRecentWeightGain.Should().Be(1.0);
        PhotoPanInertiaDefaults.GestureTranslationDecelerationDipPerMs2.Should().Be(0.0034);
        PhotoPanInertiaDefaults.GestureCrossPageTranslationDecelerationDipPerMs2.Should().Be(0.0029);
    }

    [Fact]
    public void PhotoRightClickContextMenuDefaults_ShouldMatchStabilizedValues()
    {
        PhotoRightClickContextMenuDefaults.MinThresholdDip.Should().Be(0.0);
        PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip.Should().Be(6.0);
    }

    [Fact]
    public void PhotoTransformMathDefaults_ShouldMatchStabilizedValues()
    {
        PhotoTransformMathDefaults.InverseScaleEpsilon.Should().Be(0.0001);
    }

    [Fact]
    public void PhotoTransformTimingDefaults_ShouldMatchStabilizedValues()
    {
        PhotoTransformTimingDefaults.WheelSuppressAfterGestureMs.Should().Be(180);
        PhotoTransformTimingDefaults.SmoothZoomResponseMs.Should().Be(78.0);
        PhotoTransformTimingDefaults.SmoothZoomFrameEpsilon.Should().Be(0.0005);
        PhotoTransformTimingDefaults.TransformSaveDebounceMs.Should().Be(120);
        PhotoTransformTimingDefaults.UnifiedTransformBroadcastDebounceMs.Should().Be(300);
    }

    [Fact]
    public void PhotoTransformViewportDefaults_ShouldMatchStabilizedValues()
    {
        PhotoTransformViewportDefaults.MinUsableViewportDip.Should().Be(1.0);
        PhotoTransformViewportDefaults.DefaultScale.Should().Be(1.0);
        PhotoTransformViewportDefaults.MinScale.Should().Be(0.2);
        PhotoTransformViewportDefaults.MaxScale.Should().Be(4.0);
    }

    [Fact]
    public void PhotoUnifiedTransformDefaults_ShouldMatchStabilizedValues()
    {
        PhotoUnifiedTransformDefaults.DefaultTranslateDip.Should().Be(0.0);
    }

    [Fact]
    public void PhotoZoomInputDefaults_ShouldMatchStabilizedValues()
    {
        PhotoZoomInputDefaults.WheelZoomBaseDefault.Should().Be(1.0008);
        PhotoZoomInputDefaults.WheelZoomBaseMin.Should().Be(1.0002);
        PhotoZoomInputDefaults.WheelZoomBaseMax.Should().Be(1.0020);
        PhotoZoomInputDefaults.GestureSensitivityDefault.Should().Be(1.0);
        PhotoZoomInputDefaults.GestureSensitivityMin.Should().Be(0.5);
        PhotoZoomInputDefaults.GestureSensitivityMax.Should().Be(1.8);
        PhotoZoomInputDefaults.GestureZoomNoiseThreshold.Should().Be(0.01);
        PhotoZoomInputDefaults.ZoomMinEventFactor.Should().Be(0.85);
        PhotoZoomInputDefaults.ZoomMaxEventFactor.Should().Be(1.18);
        PhotoZoomInputDefaults.ScaleApplyEpsilon.Should().Be(0.001);
        PhotoZoomInputDefaults.ManipulationTranslationEpsilonDip.Should().Be(0.01);
    }

    [Fact]
    public void PresentationRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        PresentationRuntimeDefaults.FocusMonitorIntervalMs.Should().Be(500);
        PresentationRuntimeDefaults.FocusRestoreCooldownMs.Should().Be(1200);
        PresentationRuntimeDefaults.WpsNavDebounceMs.Should().Be(200);
        PresentationRuntimeDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void PresetSchemeDefaults_ShouldMatchStabilizedValues()
    {
        PresetSchemeDefaults.Custom.Should().Be("custom");
        PresetSchemeDefaults.Balanced.Should().Be("balanced");
        PresetSchemeDefaults.Responsive.Should().Be("responsive");
        PresetSchemeDefaults.Stable.Should().Be("stable");
        PresetSchemeDefaults.DualScreen.Should().Be("dual_screen");
    }

    [Fact]
    public void StylusAdaptiveProfilingDefaults_ShouldMatchStabilizedValues()
    {
        StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMinMs.Should().Be(4);
        StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMaxMs.Should().Be(18);
        StylusAdaptiveProfilingDefaults.ObserveIntervalMinMs.Should().Be(0.2);
        StylusAdaptiveProfilingDefaults.ObserveIntervalMaxMs.Should().Be(100.0);
        StylusAdaptiveProfilingDefaults.ObserveIntervalWindowSize.Should().Be(64);
        StylusAdaptiveProfilingDefaults.ResolveRateMinSamples.Should().Be(8);
        StylusAdaptiveProfilingDefaults.HighSampleRateHzThreshold.Should().Be(150.0);
        StylusAdaptiveProfilingDefaults.MediumSampleRateHzThreshold.Should().Be(90.0);
        StylusAdaptiveProfilingDefaults.LowRatePredictionHorizonDeltaMs.Should().Be(4);
        StylusAdaptiveProfilingDefaults.MediumRatePredictionHorizonDeltaMs.Should().Be(2);
        StylusAdaptiveProfilingDefaults.HighRatePredictionHorizonDeltaMs.Should().Be(1);
        StylusAdaptiveProfilingDefaults.HighRatePredictionHorizonMinMs.Should().Be(6);
    }

    [Fact]
    public void StylusBatchTimingDefaults_ShouldMatchStabilizedValues()
    {
        StylusBatchTimingDefaults.FallbackHzWhenEmpty.Should().Be(240);
        StylusBatchTimingDefaults.MinPerSampleHz.Should().Be(480);
        StylusBatchTimingDefaults.MaxPerSampleHz.Should().Be(45);
        StylusBatchTimingDefaults.FallbackSpanHz.Should().Be(120);
    }

    [Fact]
    public void StylusInterpolationDefaults_ShouldMatchStabilizedValues()
    {
        StylusInterpolationDefaults.MinDtMsForSpeed.Should().Be(0.2);
        StylusInterpolationDefaults.SpeedNormBase.Should().Be(0.9);
        StylusInterpolationDefaults.SpeedNormRange.Should().Be(2.4);
        StylusInterpolationDefaults.StepScaleBase.Should().Be(0.9);
        StylusInterpolationDefaults.StepScaleSpeedMultiplier.Should().Be(0.55);
        StylusInterpolationDefaults.InterpolationStepMinDip.Should().Be(3.0);
        StylusInterpolationDefaults.InterpolationStepMaxDip.Should().Be(12.0);
        StylusInterpolationDefaults.DistanceTriggerMultiplier.Should().Be(1.4);
        StylusInterpolationDefaults.FastSpeedThreshold.Should().Be(3.2);
        StylusInterpolationDefaults.MediumSpeedThreshold.Should().Be(2.2);
        StylusInterpolationDefaults.SlowSpeedThreshold.Should().Be(1.4);
        StylusInterpolationDefaults.FastSpeedMaxSegments.Should().Be(4);
        StylusInterpolationDefaults.MediumSpeedMaxSegments.Should().Be(5);
        StylusInterpolationDefaults.SlowSpeedMaxSegments.Should().Be(6);
        StylusInterpolationDefaults.DefaultMaxSegments.Should().Be(7);
        StylusInterpolationDefaults.MinSegmentCount.Should().Be(2);
        StylusInterpolationDefaults.SlowFrameDtThresholdMs.Should().Be(10.0);
        StylusInterpolationDefaults.SlowFrameMaxSegmentsBonus.Should().Be(1);
        StylusInterpolationDefaults.MaxSegmentsCap.Should().Be(8);
        StylusInterpolationDefaults.SegmentProgressUpperBound.Should().Be(1.0);
        StylusInterpolationDefaults.MinTimestampStepTicks.Should().Be(1);
    }

    [Fact]
    public void StylusPressureAnalysisDefaults_ShouldMatchStabilizedValues()
    {
        StylusPressureAnalysisDefaults.WindowSize.Should().Be(28);
        StylusPressureAnalysisDefaults.MinSamplesForProfile.Should().Be(12);
        StylusPressureAnalysisDefaults.EndpointPseudoRatioThreshold.Should().Be(0.82);
        StylusPressureAnalysisDefaults.LowRangeThreshold.Should().Be(0.07);
        StylusPressureAnalysisDefaults.ContinuousRangeThreshold.Should().Be(0.18);
        StylusPressureAnalysisDefaults.EndpointDistinctMax.Should().Be(3);
        StylusPressureAnalysisDefaults.LowRangeDistinctMax.Should().Be(4);
        StylusPressureAnalysisDefaults.ContinuousDistinctMin.Should().Be(7);
        StylusPressureAnalysisDefaults.BucketScale.Should().Be(100.0);
        StylusPressureAnalysisDefaults.EndpointRatioUpperBoundForContinuous.Should().Be(0.7);
        StylusPressureAnalysisDefaults.GammaMin.Should().Be(0.55);
        StylusPressureAnalysisDefaults.GammaMax.Should().Be(1.8);
    }

    [Fact]
    public void StylusPressureCalibrationDefaults_ShouldMatchStabilizedValues()
    {
        StylusPressureCalibrationDefaults.BinCount.Should().Be(64);
        StylusPressureCalibrationDefaults.MinSamplesForQuantiles.Should().Be(20);
        StylusPressureCalibrationDefaults.SeedRangeMinWidth.Should().Be(0.01);
        StylusPressureCalibrationDefaults.EmaAlpha.Should().Be(0.03);
        StylusPressureCalibrationDefaults.LowQuantile.Should().Be(0.04);
        StylusPressureCalibrationDefaults.HighQuantile.Should().Be(0.96);
        StylusPressureCalibrationDefaults.MinEffectiveRange.Should().Be(0.04);
    }

    [Fact]
    public void StylusRuntimeDefaults_ShouldMatchStabilizedValues()
    {
        StylusRuntimeDefaults.PressureGammaStable.Should().Be(1.16);
        StylusRuntimeDefaults.PressureGammaResponsive.Should().Be(0.88);
        StylusRuntimeDefaults.PressureGammaDefault.Should().Be(1.0);
        StylusRuntimeDefaults.CalibratedRangeSeedMinWidth.Should().Be(0.01);
        StylusRuntimeDefaults.CalibratedLowDefault.Should().Be(0.0);
        StylusRuntimeDefaults.CalibratedHighDefault.Should().Be(1.0);
    }

    [Fact]
    public void ToolbarScaleDefaults_ShouldMatchStabilizedValues()
    {
        ToolbarScaleDefaults.Min.Should().Be(0.8);
        ToolbarScaleDefaults.Default.Should().Be(1.0);
        ToolbarScaleDefaults.Max.Should().Be(2.0);
    }

    [Fact]
    public void WpsInputModeDefaults_ShouldMatchStabilizedValues()
    {
        WpsInputModeDefaults.Auto.Should().Be("auto");
        WpsInputModeDefaults.Raw.Should().Be("raw");
        WpsInputModeDefaults.Message.Should().Be("message");
    }

}
