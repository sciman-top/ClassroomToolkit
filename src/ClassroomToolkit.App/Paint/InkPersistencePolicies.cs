using ClassroomToolkit.App.Ink;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System;

namespace ClassroomToolkit.App.Paint;

internal static class InkCacheRuntimeDefaults
{
    internal const int HistoryLimit = 20;
    internal const long MaxHistoryMemoryBytes = 512L * 1024L * 1024L;
    internal const int NoiseTileCacheLimit = 96;
    internal const int SolidBrushCacheLimit = 256;
    internal const int PenCacheLimit = 192;
}

internal readonly record struct InkCacheUpdateTransitionPlan(
    bool ShouldStartMonitor,
    bool ShouldClearCache,
    bool ShouldRequestRefresh);

internal static class InkExportSnapshotBuilder
{
    internal static bool HasAnyInkStrokes(InkDocumentData? inkDoc)
    {
        if (inkDoc?.Pages == null || inkDoc.Pages.Count == 0)
        {
            return false;
        }

        foreach (var page in inkDoc.Pages)
        {
            if (page?.Strokes?.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static void ApplyScopeFilter(
        InkDocumentData inkDoc,
        InkExportScope scope,
        Func<InkPageData, bool> keepPage)
    {
        InkPayloadNormalizer.NormalizeDocument(inkDoc);
        if (scope != InkExportScope.SessionChangesOnly)
        {
            return;
        }
        if (inkDoc.Pages.Count == 0)
        {
            return;
        }

        inkDoc.Pages = inkDoc.Pages.Where(keepPage).ToList();
    }

    internal static void UpsertPageStrokes(
        InkDocumentData inkDoc,
        string sourceFilePath,
        int pageIndex,
        List<InkStrokeData> strokes)
    {
        InkPayloadNormalizer.NormalizeDocument(inkDoc);
        strokes = InkPayloadNormalizer.NormalizeStrokes(strokes);
        var currentPage = inkDoc.Pages.Find(p => p.PageIndex == pageIndex);
        if (strokes.Count == 0)
        {
            if (currentPage != null)
            {
                inkDoc.Pages.Remove(currentPage);
            }
            return;
        }

        if (currentPage == null)
        {
            currentPage = new InkPageData
            {
                PageIndex = pageIndex,
                SourcePath = sourceFilePath,
                DocumentName = Path.GetFileNameWithoutExtension(sourceFilePath)
            };
            inkDoc.Pages.Add(currentPage);
        }

        currentPage.Strokes = strokes;
        currentPage.UpdatedAt = DateTime.UtcNow;
    }

    internal static void MergeCachedPages(
        InkDocumentData inkDoc,
        string sourceFilePath,
        IReadOnlyList<(string Key, List<InkStrokeData> Strokes)> cacheSnapshot,
        Func<IEnumerable<InkStrokeData>, List<InkStrokeData>> cloneStrokes)
    {
        foreach (var entry in cacheSnapshot)
        {
            if (!TryParseCacheKey(entry.Key, out var cachedSourcePath, out var pageIndex))
            {
                continue;
            }
            if (!string.Equals(cachedSourcePath, sourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            UpsertPageStrokes(inkDoc, sourceFilePath, pageIndex, cloneStrokes(entry.Strokes ?? new List<InkStrokeData>()));
        }
    }

    internal static InkDocumentData? CloneDocument(
        InkDocumentData? source,
        Func<IEnumerable<InkStrokeData>, List<InkStrokeData>> cloneStrokes)
    {
        if (source == null)
        {
            return null;
        }

        InkPayloadNormalizer.NormalizeDocument(source);
        var clone = new InkDocumentData
        {
            Version = source.Version,
            SourcePath = source.SourcePath
        };

        foreach (var page in source.Pages)
        {
            clone.Pages.Add(new InkPageData
            {
                PageIndex = page.PageIndex,
                DocumentName = page.DocumentName,
                SourcePath = page.SourcePath,
                BackgroundImageFile = page.BackgroundImageFile,
                CreatedAt = page.CreatedAt,
                UpdatedAt = page.UpdatedAt,
                Strokes = cloneStrokes(page.Strokes ?? new List<InkStrokeData>())
            });
        }

        return clone;
    }

    internal static IEnumerable<string> EnumerateCachedInkSourcesInDirectory(
        IReadOnlyList<(string Key, List<InkStrokeData> Strokes)> cacheSnapshot,
        string directoryPath)
    {
        foreach (var entry in cacheSnapshot)
        {
            if (entry.Strokes == null || entry.Strokes.Count == 0)
            {
                continue;
            }
            if (!TryParseCacheKey(entry.Key, out var sourcePath, out _))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }
            if (!string.Equals(Path.GetDirectoryName(sourcePath), directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            yield return sourcePath;
        }
    }

    internal static bool TryParseCacheKey(string cacheKey, out string sourcePath, out int pageIndex)
    {
        sourcePath = string.Empty;
        pageIndex = 1;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        if (cacheKey.StartsWith("img|", StringComparison.Ordinal))
        {
            sourcePath = cacheKey["img|".Length..];
            pageIndex = 1;
            return !string.IsNullOrWhiteSpace(sourcePath);
        }

        if (cacheKey.StartsWith("pdf|", StringComparison.Ordinal))
        {
            const string marker = "|page_";
            var markerIndex = cacheKey.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex <= "pdf|".Length)
            {
                return false;
            }

            var pathPart = cacheKey["pdf|".Length..markerIndex];
            var pagePart = cacheKey[(markerIndex + marker.Length)..];
            if (!int.TryParse(pagePart, out pageIndex) || pageIndex <= 0)
            {
                return false;
            }

            sourcePath = pathPart;
            return !string.IsNullOrWhiteSpace(sourcePath);
        }

        return false;
    }
}

internal static class InkGeometryDefaults
{
    internal const double MinSelectionRectSideDip = 1.0;
    internal const double MinShapeStrokeThicknessDip = 1.0;
    internal const double MinShapeRectSideDip = 1.0;
    internal const double MinPenThicknessDip = 1.0;
    internal const double MinEraserRadiusDip = 2.0;
    internal const double EraserMoveThresholdMinDip = 1.0;
    internal const double EraserMoveThresholdScale = 0.2;
    internal const double EraserTapDistanceThresholdDip = 0.5;
}

internal static class InkPredictionDefaults
{
    internal const int HorizonMinMs = 4;
    internal const int HorizonMaxMs = 16;
    internal const double MaxDistanceDip = 10.0;
    internal const int PrimaryAlphaMin = 24;
    internal const int PrimaryAlphaMax = 136;
    internal const int SecondaryAlphaMin = 18;
    internal const int SecondaryAlphaMax = 110;
    internal const int TipAlphaMin = 14;
    internal const int TipAlphaMax = 92;
}

internal static class InkRenderBatchingDefaults
{
    internal const int ProximityPaddingPixels = 24;
    internal const double AreaRatioThreshold = 1.6;
}

internal static class InkRenderingCacheDefaults
{
    internal const int PenWidthMinMilli = 1;
    internal const double PenWidthQuantizeScale = 1000.0;
    internal const int OpacityMaskCacheLimit = 128;
    internal const int StrokeColorCacheLimit = 128;
}

internal sealed class InkRuntimeDiagnostics
{
    private readonly bool _inkRedrawTelemetryEnabled;

    private InkRuntimeDiagnostics(bool inkRedrawTelemetryEnabled)
    {
        _inkRedrawTelemetryEnabled = inkRedrawTelemetryEnabled;
    }

    internal static InkRuntimeDiagnostics? CreateFromEnvironment()
    {
        var inkRedrawTelemetryEnabled = InkRedrawTelemetryPolicies.ResolveEnabledFromEnvironment();
        if (!inkRedrawTelemetryEnabled)
        {
            return null;
        }

        return new InkRuntimeDiagnostics(inkRedrawTelemetryEnabled);
    }

    internal void OnInkInput()
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnRedrawRequested(bool throttled)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnRedrawCompleted(double elapsedMs)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnAutoSaveDeferred(string reason)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnAutoSavePersistResult(bool persisted)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnAutoSaveFailure()
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnSyncPersist()
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnCrossPageFirstInputEvent(long traceId, string stage, double elapsedMs, string? details = null)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnCrossPageUpdateEvent(string stage, string source, string? details = null)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnInkRedrawTelemetry(
        int sampleCount,
        double partialHitRate,
        int windowCount,
        int windowSize,
        double allP50Ms,
        double allP95Ms,
        double partialP95Ms,
        double fullP95Ms)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[InkRedrawTelemetry] samples={sampleCount} partial-hit={partialHitRate:F1}% " +
            $"window={windowCount}/{windowSize} " +
            $"all(p50/p95)={allP50Ms:F2}/{allP95Ms:F2}ms " +
            $"partial(p95)={partialP95Ms:F2}ms full(p95)={fullP95Ms:F2}ms");
    }
}

internal static class InkRuntimeTimingDefaults
{
    internal const int CalligraphyPreviewMinIntervalMs = 16;
    internal const int InputCooldownMs = 120;
    internal const int MonitorActiveIntervalMs = 600;
    internal const int MonitorIdleIntervalMs = 1400;
    internal const int IdleThresholdMs = 2500;
    internal const int RedrawMinIntervalMs = 16;
    internal const double PhotoPanRedrawThresholdDip = 3.0;
    internal const int RedrawDispatchDelayMinMs = 1;
    internal const int SidecarAutoSaveDelayMs = 600;
    internal const int SidecarAutoSaveRetryMax = 3;
    internal const int SidecarAutoSaveRetryDelayMs = 900;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}

internal readonly record struct InkSaveUpdateTransitionPlan(
    bool ShouldStopAutoSaveTimer,
    bool ShouldCancelPendingAutoSave,
    bool ShouldScheduleAutoSave);

internal readonly record struct InkShowUpdateTransitionPlan(
    bool ShouldApplySetting,
    bool ShouldReturnAfterSetting,
    bool ShouldClearInkState,
    bool ShouldLoadCurrentPage,
    bool RequestCrossPageUpdateForEnabled,
    bool RequestCrossPageUpdateForDisabled);

internal static class InkStrokeEraseUpdater
{
    internal static bool TryApplyUpdatedGeometryPath(InkStrokeData stroke, string? updatedPath, out bool removed)
    {
        removed = false;
        if (updatedPath == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(updatedPath))
        {
            removed = true;
            return true;
        }

        if (string.Equals(updatedPath, stroke.GeometryPath, StringComparison.Ordinal))
        {
            return false;
        }

        stroke.GeometryPath = updatedPath;
        stroke.CachedGeometry = null;
        stroke.CachedBounds = null;
        stroke.CachedRibbonGeometries = null;
        return true;
    }
}

internal static partial class InkPersistencePolicies
{
    internal static bool ShouldPersistSnapshot(
        bool runtimeStateKnown,
        string runtimeHash,
        string snapshotHash)
    {
        if (!runtimeStateKnown)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(runtimeHash)
            || string.Equals(runtimeHash, snapshotHash, StringComparison.Ordinal);
    }

    internal static InkCacheUpdateTransitionPlan ResolveInkCacheUpdateTransition(bool enabled, bool monitorEnabled)
    {
        return new InkCacheUpdateTransitionPlan(
            ShouldStartMonitor: !monitorEnabled,
            ShouldClearCache: !enabled,
            ShouldRequestRefresh: true);
    }
}

internal static class InkRedrawPolicies
{
    internal static bool ShouldUsePartialClear(
        bool clipAvailable,
        Int32Rect clipPixelRect,
        Int32Rect? lastClipPixelRect)
    {
        return clipAvailable
            && lastClipPixelRect.HasValue
            && lastClipPixelRect.Value.Equals(clipPixelRect);
    }

    internal static bool TryResolvePixelClip(
        Rect clipBoundsDip,
        int surfacePixelWidth,
        int surfacePixelHeight,
        double surfaceDpiX,
        double surfaceDpiY,
        out Int32Rect clipPixelRect)
    {
        clipPixelRect = default;
        if (clipBoundsDip.IsEmpty || clipBoundsDip.Width <= 0 || clipBoundsDip.Height <= 0)
        {
            return false;
        }

        var dpiScaleX = surfaceDpiX > 0 ? surfaceDpiX / 96.0 : 1.0;
        var dpiScaleY = surfaceDpiY > 0 ? surfaceDpiY / 96.0 : 1.0;
        var left = (int)Math.Floor(clipBoundsDip.Left * dpiScaleX);
        var top = (int)Math.Floor(clipBoundsDip.Top * dpiScaleY);
        var right = (int)Math.Ceiling(clipBoundsDip.Right * dpiScaleX);
        var bottom = (int)Math.Ceiling(clipBoundsDip.Bottom * dpiScaleY);

        left = Math.Clamp(left, 0, surfacePixelWidth);
        top = Math.Clamp(top, 0, surfacePixelHeight);
        right = Math.Clamp(right, 0, surfacePixelWidth);
        bottom = Math.Clamp(bottom, 0, surfacePixelHeight);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        clipPixelRect = new Int32Rect(left, top, width, height);
        return true;
    }
}

internal static class InkRedrawTelemetryPolicies
{
    internal const string EnvironmentFlagName = "CTK_INK_REDRAW_TELEMETRY";

    internal static bool ResolveEnabledFromEnvironment()
    {
        return IsEnabledValue(Environment.GetEnvironmentVariable(EnvironmentFlagName));
    }

    internal static bool IsEnabledValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "1" => true,
            "TRUE" => true,
            "ON" => true,
            "YES" => true,
            "ENABLED" => true,
            _ => false
        };
    }

    internal static void AppendSample(Queue<double> samples, double value, int windowSize)
    {
        if (samples == null || windowSize <= 0 || !double.IsFinite(value))
        {
            return;
        }

        samples.Enqueue(value);
        while (samples.Count > windowSize)
        {
            samples.Dequeue();
        }
    }

    internal static double Percentile(IReadOnlyCollection<double> samples, double percentile)
    {
        if (samples == null || samples.Count == 0)
        {
            return 0;
        }

        var p = Math.Clamp(percentile, 0.0, 1.0);
        var sorted = samples.OrderBy(x => x).ToArray();
        var index = (int)Math.Floor((sorted.Length - 1) * p);
        return sorted[index];
    }

    internal static bool ShouldEmitLog(
        int sampleCount,
        DateTime nowUtc,
        DateTime lastLogUtc,
        int minSampleStride,
        double minIntervalSeconds)
    {
        if (sampleCount <= 0)
        {
            return false;
        }

        if (sampleCount < minSampleStride && (nowUtc - lastLogUtc).TotalSeconds < minIntervalSeconds)
        {
            return false;
        }

        if (sampleCount % minSampleStride != 0 && (nowUtc - lastLogUtc).TotalSeconds < minIntervalSeconds)
        {
            return false;
        }

        return true;
    }
}

internal static partial class InkPersistencePolicies
{
    internal static InkSaveUpdateTransitionPlan ResolveInkSaveUpdateTransition(bool enabled)
    {
        return enabled
            ? new InkSaveUpdateTransitionPlan(
                ShouldStopAutoSaveTimer: false,
                ShouldCancelPendingAutoSave: false,
                ShouldScheduleAutoSave: true)
            : new InkSaveUpdateTransitionPlan(
                ShouldStopAutoSaveTimer: true,
                ShouldCancelPendingAutoSave: true,
                ShouldScheduleAutoSave: false);
    }

    internal static InkShowUpdateTransitionPlan ResolveInkShowUpdateTransition(
        bool currentInkShowEnabled,
        bool nextInkShowEnabled,
        bool photoModeActive)
    {
        if (currentInkShowEnabled == nextInkShowEnabled)
        {
            return new InkShowUpdateTransitionPlan(
                ShouldApplySetting: false,
                ShouldReturnAfterSetting: true,
                ShouldClearInkState: false,
                ShouldLoadCurrentPage: false,
                RequestCrossPageUpdateForEnabled: false,
                RequestCrossPageUpdateForDisabled: false);
        }

        if (!photoModeActive)
        {
            return new InkShowUpdateTransitionPlan(
                ShouldApplySetting: true,
                ShouldReturnAfterSetting: true,
                ShouldClearInkState: false,
                ShouldLoadCurrentPage: false,
                RequestCrossPageUpdateForEnabled: false,
                RequestCrossPageUpdateForDisabled: false);
        }

        if (!nextInkShowEnabled)
        {
            return new InkShowUpdateTransitionPlan(
                ShouldApplySetting: true,
                ShouldReturnAfterSetting: false,
                ShouldClearInkState: true,
                ShouldLoadCurrentPage: false,
                RequestCrossPageUpdateForEnabled: false,
                RequestCrossPageUpdateForDisabled: true);
        }

        return new InkShowUpdateTransitionPlan(
            ShouldApplySetting: true,
            ShouldReturnAfterSetting: false,
            ShouldClearInkState: false,
            ShouldLoadCurrentPage: true,
            RequestCrossPageUpdateForEnabled: true,
            RequestCrossPageUpdateForDisabled: false);
    }

    internal static bool ShouldApplyLoadedSnapshot(
        bool runtimeStateKnown,
        string runtimeHash,
        bool runtimeDirty,
        string loadedHash)
    {
        if (!runtimeStateKnown || string.IsNullOrWhiteSpace(runtimeHash))
        {
            return true;
        }

        var hashMatched = string.Equals(runtimeHash, loadedHash, StringComparison.Ordinal);
        if (hashMatched)
        {
            return true;
        }

        if (runtimeDirty)
        {
            return false;
        }

        return !string.Equals(runtimeHash, "empty", StringComparison.Ordinal);
    }
}

internal static class InkUndoPolicies
{
    internal static bool ShouldTrackVectorSnapshot(bool inkRecordEnabled, bool photoInkModeActive)
    {
        return inkRecordEnabled || photoInkModeActive;
    }

    internal static bool ShouldPreferGlobalPhotoUndo(bool photoModeActive, int globalHistoryCount)
    {
        return photoModeActive && globalHistoryCount > 0;
    }

    internal static bool ShouldPreferLocalVectorUndo(
        bool inkRecordEnabled,
        bool photoInkModeActive,
        int localHistoryCount)
    {
        return localHistoryCount > 0
            && ShouldTrackVectorSnapshot(inkRecordEnabled, photoInkModeActive);
    }
}
