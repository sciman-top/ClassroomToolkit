using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace ClassroomToolkit.App.Paint;

internal enum BoardPrimaryAction
{
    CaptureRegion,
    EnterWhiteboard
}

public enum CalligraphyRenderMode
{
    Clarity = 0,
    Ink = 1
}

public enum ClassroomWritingMode
{
    Stable = 0,
    Balanced,
    Responsive
}

internal readonly record struct ClassroomWritingProfile(
    double MarkerPressureMultiplier,
    double CalligraphyPressureInfluenceMultiplier,
    double CalligraphyPressureScaleMultiplier,
    double PseudoPressureLowThreshold,
    double PseudoPressureHighThreshold,
    double CalligraphyPreviewMinDistance);

internal readonly record struct ClassroomRuntimeSettings(
    double PseudoPressureLowThreshold,
    double PseudoPressureHighThreshold,
    double CalligraphyPreviewMinDistance);

internal static class ClassroomWritingModeTuner
{
    public const double DefaultCalligraphyPreviewMinDistance = 2.0;
    public const double DefaultPseudoPressureLowThreshold = 0.0001;
    public const double DefaultPseudoPressureHighThreshold = 0.9999;

    private const double MarkerPressureFactorMin = 0.02;
    private const double MarkerPressureFactorMax = 0.45;
    private const double CalligraphyInfluenceMin = 0.2;
    private const double CalligraphyInfluenceMax = 0.9;
    private const double CalligraphyScaleMin = 0.12;
    private const double CalligraphyScaleMax = 0.55;
    private const double PseudoPressureLowMin = 0.0;
    private const double PseudoPressureLowMax = 0.49;
    private const double PseudoPressureHighMinGap = 0.001;
    private const double PseudoPressureHighMax = 1.0;
    private const double CalligraphyPreviewMin = 1.0;
    private const double CalligraphyPreviewMax = 4.0;

    public static ClassroomWritingProfile ResolveProfile(ClassroomWritingMode mode)
    {
        return mode switch
        {
            ClassroomWritingMode.Stable => new ClassroomWritingProfile(
                MarkerPressureMultiplier: 0.85,
                CalligraphyPressureInfluenceMultiplier: 0.7,
                CalligraphyPressureScaleMultiplier: 0.66,
                PseudoPressureLowThreshold: 0.0002,
                PseudoPressureHighThreshold: 0.9998,
                CalligraphyPreviewMinDistance: 2.6),
            ClassroomWritingMode.Responsive => new ClassroomWritingProfile(
                MarkerPressureMultiplier: 1.18,
                CalligraphyPressureInfluenceMultiplier: 1.3,
                CalligraphyPressureScaleMultiplier: 1.28,
                PseudoPressureLowThreshold: 0.00005,
                PseudoPressureHighThreshold: 0.99995,
                CalligraphyPreviewMinDistance: 1.4),
            _ => new ClassroomWritingProfile(
                MarkerPressureMultiplier: 1.0,
                CalligraphyPressureInfluenceMultiplier: 1.0,
                CalligraphyPressureScaleMultiplier: 1.0,
                PseudoPressureLowThreshold: DefaultPseudoPressureLowThreshold,
                PseudoPressureHighThreshold: DefaultPseudoPressureHighThreshold,
                CalligraphyPreviewMinDistance: DefaultCalligraphyPreviewMinDistance)
        };
    }

    public static ClassroomRuntimeSettings ResolveRuntimeSettings(ClassroomWritingMode mode)
    {
        var profile = ResolveProfile(mode);
        var low = Math.Clamp(profile.PseudoPressureLowThreshold, PseudoPressureLowMin, PseudoPressureLowMax);
        var high = Math.Clamp(profile.PseudoPressureHighThreshold, low + PseudoPressureHighMinGap, PseudoPressureHighMax);
        var previewDistance = Math.Clamp(profile.CalligraphyPreviewMinDistance, CalligraphyPreviewMin, CalligraphyPreviewMax);
        return new ClassroomRuntimeSettings(low, high, previewDistance);
    }

    public static void ApplyToMarkerConfig(MarkerBrushConfig config, ClassroomWritingMode mode)
    {
        ArgumentNullException.ThrowIfNull(config);

        var profile = ResolveProfile(mode);
        config.PressureWidthFactor = Math.Clamp(
            config.PressureWidthFactor * profile.MarkerPressureMultiplier,
            MarkerPressureFactorMin,
            MarkerPressureFactorMax);
    }

    public static void ApplyToCalligraphyConfig(BrushPhysicsConfig config, ClassroomWritingMode mode)
    {
        ArgumentNullException.ThrowIfNull(config);

        var profile = ResolveProfile(mode);
        config.RealPressureWidthInfluence = Math.Clamp(
            config.RealPressureWidthInfluence * profile.CalligraphyPressureInfluenceMultiplier,
            CalligraphyInfluenceMin,
            CalligraphyInfluenceMax);
        config.RealPressureWidthScale = Math.Clamp(
            config.RealPressureWidthScale * profile.CalligraphyPressureScaleMultiplier,
            CalligraphyScaleMin,
            CalligraphyScaleMax);
    }

    public static bool TryResolveStylusPressure(
        double rawPressure,
        double lowThreshold,
        double highThreshold,
        out double resolvedPressure)
    {
        resolvedPressure = 0.0;
        if (!double.IsFinite(rawPressure))
        {
            return false;
        }

        var low = Math.Clamp(lowThreshold, PseudoPressureLowMin, PseudoPressureLowMax);
        var high = Math.Clamp(highThreshold, low + PseudoPressureHighMinGap, PseudoPressureHighMax);
        if (rawPressure <= low || rawPressure >= high)
        {
            return false;
        }

        resolvedPressure = Math.Clamp(rawPressure, 0.0, 1.0);
        return true;
    }
}

public interface IPaintWindowFactory
{
    (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create();
}

internal static class PaintActionInvoker
{
    internal static void TryInvoke(Action action)
    {
        _ = SafeActionExecutionExecutor.TryExecute(action);
    }

    internal static TResult TryInvoke<TResult>(
        Func<TResult> action,
        TResult fallback = default!,
        Action<Exception>? onFailure = null)
    {
        return SafeActionExecutionExecutor.TryExecute(action, fallback, onFailure);
    }
}

public enum PaintBrushStyle
{
    Standard = 0,
    StandardRibbon,
    Calligraphy
}

internal static class PaintSettingsDefaults
{
    internal const double DoubleComparisonEpsilon = 0.0001;
    internal const double ComboTagComparisonEpsilon = 0.001;
    internal const double PercentMin = 0.0;
    internal const double PercentMax = 100.0;
    internal const double PercentToByteScale = 255.0;
}

internal static class PaintSettingsOptionDefaults
{
    internal const double EraserSizeDefault = 24.0;
    internal const double EraserSizeMin = 6.0;
    internal const double EraserSizeMax = 60.0;
    internal const int InkExportMaxParallelDefault = 2;
    internal const int PhotoNeighborPrefetchRadiusDefault = 4;
}

public enum PaintShapeType
{
    None = 0,
    Line,
    DashedLine,
    Arrow,
    DashedArrow,
    Rectangle,
    RectangleFill,
    Ellipse,
    Triangle,
    Path
}

public enum PaintToolMode
{
    Cursor = 0,
    Brush,
    Eraser,
    Shape,
    RegionErase
}

internal sealed class PaintToolSelectionManager
{
    private readonly Stack<PaintToolMode> _history = new();

    public PaintToolSelectionManager(PaintToolMode initialMode = PaintToolMode.Brush)
    {
        CurrentMode = initialMode;
    }

    public PaintToolMode CurrentMode { get; private set; }

    public PaintToolMode Select(PaintToolMode requestedMode, bool allowToggleOffCurrent)
    {
        if (requestedMode == CurrentMode)
        {
            if (!allowToggleOffCurrent)
            {
                return CurrentMode;
            }

            CurrentMode = ResolveFallbackMode();
            return CurrentMode;
        }

        PushHistory(CurrentMode);
        CurrentMode = requestedMode;
        return CurrentMode;
    }

    public void Reset(PaintToolMode mode)
    {
        _history.Clear();
        CurrentMode = mode;
    }

    private PaintToolMode ResolveFallbackMode()
    {
        while (_history.Count > 0)
        {
            var candidate = _history.Pop();
            if (candidate != CurrentMode && IsHistoryMode(candidate))
            {
                return candidate;
            }
        }

        return PaintToolMode.Brush;
    }

    private void PushHistory(PaintToolMode mode)
    {
        if (!IsHistoryMode(mode))
        {
            return;
        }

        if (_history.Count > 0 && _history.Peek() == mode)
        {
            return;
        }

        _history.Push(mode);
    }

    private static bool IsHistoryMode(PaintToolMode mode)
    {
        return mode != PaintToolMode.Cursor;
    }
}

internal static class PaintToolSizePolicy
{
    internal const double BrushSizeMinimum = 1.0;
    internal const double BrushSizeDefault = 12.0;
    internal const double EraserSizeMinimum = 4.0;
    internal const double EraserSizeDefault = 24.0;

    internal static double NormalizeBrushSize(double requestedSize, double currentSize)
    {
        return Normalize(requestedSize, currentSize, BrushSizeMinimum, BrushSizeDefault);
    }

    internal static double NormalizeEraserSize(double requestedSize, double currentSize)
    {
        return Normalize(requestedSize, currentSize, EraserSizeMinimum, EraserSizeDefault);
    }

    private static double Normalize(double requestedSize, double currentSize, double minimum, double fallback)
    {
        if (double.IsFinite(requestedSize))
        {
            return Math.Max(minimum, requestedSize);
        }

        return double.IsFinite(currentSize)
            ? Math.Max(minimum, currentSize)
            : fallback;
    }
}

internal sealed class PaintWindowFactory : IPaintWindowFactory
{
    private readonly InkPersistenceService _persistence;
    private readonly InkExportService _export;
    private readonly InkExportOptions _exportOptions;
    private readonly ILogger<PaintOverlayWindow> _overlayLogger;
    private readonly IInkHistorySnapshotStore? _inkHistorySnapshotStore;

    public PaintWindowFactory(
        InkPersistenceService persistence,
        InkExportService export,
        InkExportOptions exportOptions,
        ILogger<PaintOverlayWindow> overlayLogger,
        IInkHistorySnapshotStore? inkHistorySnapshotStore = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(exportOptions);
        ArgumentNullException.ThrowIfNull(overlayLogger);

        _persistence = persistence;
        _export = export;
        _exportOptions = exportOptions;
        _overlayLogger = overlayLogger;
        _inkHistorySnapshotStore = inkHistorySnapshotStore;
    }

    public (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create()
    {
        var overlay = new PaintOverlayWindow(_overlayLogger);
        Debug.WriteLine(
            $"[Storage] InkHistory backend selected={(_inkHistorySnapshotStore != null ? "Sqlite" : "Sidecar")}");
        overlay.SetInkPersistenceServices(_persistence, _export, _exportOptions, _inkHistorySnapshotStore);
        return (overlay, new PaintToolbarWindow());
    }
}

internal enum SceneCardsLayoutMode
{
    TwoColumns = 0,
    SingleColumn = 1
}

internal static class SceneCardsLayoutPolicy
{
    internal const double SingleColumnThreshold = 860;

    internal static SceneCardsLayoutMode Resolve(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth))
        {
            return SceneCardsLayoutMode.TwoColumns;
        }

        return availableWidth < SingleColumnThreshold
            ? SceneCardsLayoutMode.SingleColumn
            : SceneCardsLayoutMode.TwoColumns;
    }
}
