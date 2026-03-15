using System;
using System.Collections.Generic;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkStrokeApplyExecutionResult(
    int AppliedStrokeCount,
    bool UsedInteractiveFastPathCopy,
    bool RedrewInkSurface,
    bool MarkedCurrentPageLoaded);

internal static class InkStrokeApplyCoordinator
{
    internal static InkStrokeApplyExecutionResult Apply(
        IReadOnlyList<InkStrokeData> strokes,
        bool preferInteractiveFastPath,
        Action clearRuntimeStrokes,
        Action<IReadOnlyList<InkStrokeData>> addRuntimeStrokes,
        Func<IReadOnlyList<InkStrokeData>, bool, bool> tryApplyNeighborInkBitmapForCurrentPage,
        Action redrawInkSurface,
        Action finalizeFastAppliedInkSurface,
        Action markCurrentInkPageLoaded,
        Action<double, bool> recordPerfMilliseconds,
        Func<double> getElapsedMilliseconds,
        Func<bool> dispatcherCheckAccess,
        Action<string, string?>? markTraceStage = null)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        ArgumentNullException.ThrowIfNull(clearRuntimeStrokes);
        ArgumentNullException.ThrowIfNull(addRuntimeStrokes);
        ArgumentNullException.ThrowIfNull(tryApplyNeighborInkBitmapForCurrentPage);
        ArgumentNullException.ThrowIfNull(redrawInkSurface);
        ArgumentNullException.ThrowIfNull(finalizeFastAppliedInkSurface);
        ArgumentNullException.ThrowIfNull(markCurrentInkPageLoaded);
        ArgumentNullException.ThrowIfNull(recordPerfMilliseconds);
        ArgumentNullException.ThrowIfNull(getElapsedMilliseconds);
        ArgumentNullException.ThrowIfNull(dispatcherCheckAccess);

        markTraceStage?.Invoke(
            "apply-enter",
            $"strokes={strokes.Count} preferFast={preferInteractiveFastPath}");

        clearRuntimeStrokes();
        addRuntimeStrokes(strokes);

        var fastApplied = tryApplyNeighborInkBitmapForCurrentPage(strokes, preferInteractiveFastPath);
        if (!fastApplied)
        {
            markTraceStage?.Invoke("apply-redraw", null);
            redrawInkSurface();
        }
        else
        {
            markTraceStage?.Invoke("apply-fast-bitmap", null);
            finalizeFastAppliedInkSurface();
        }

        markCurrentInkPageLoaded();
        recordPerfMilliseconds(getElapsedMilliseconds(), dispatcherCheckAccess());
        markTraceStage?.Invoke("apply-exit", $"ms={getElapsedMilliseconds():F2}");

        return new InkStrokeApplyExecutionResult(
            AppliedStrokeCount: strokes.Count,
            UsedInteractiveFastPathCopy: fastApplied,
            RedrewInkSurface: !fastApplied,
            MarkedCurrentPageLoaded: true);
    }
}
