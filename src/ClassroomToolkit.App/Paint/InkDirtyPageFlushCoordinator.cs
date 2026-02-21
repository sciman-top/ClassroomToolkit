using System;
using System.Collections.Generic;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

internal static class InkDirtyPageFlushCoordinator
{
    internal delegate bool TryGetPageStrokesDelegate(string sourcePath, int pageIndex, out List<InkStrokeData> strokes);
    internal delegate bool PersistPageDelegate(string sourcePath, int pageIndex, List<InkStrokeData> strokes, out string? errorMessage);

    internal sealed class FlushResult
    {
        public int AttemptedCount { get; set; }
        public int SucceededCount { get; set; }
        public List<(string SourcePath, int PageIndex, string Error)> Failures { get; } = new();
        public bool IsSuccess => Failures.Count == 0;
    }

    internal static FlushResult Flush(
        bool inkSaveEnabled,
        string? directoryPath,
        Action stopAutoSaveTimer,
        Action cancelAutoSaveGeneration,
        Action finalizeActiveOperation,
        Func<string?, IReadOnlyList<(string SourcePath, int PageIndex)>> getDirtyPages,
        TryGetPageStrokesDelegate tryGetPageStrokes,
        PersistPageDelegate persistPage)
    {
        var result = new FlushResult();
        if (!inkSaveEnabled)
        {
            return result;
        }

        stopAutoSaveTimer();
        cancelAutoSaveGeneration();
        finalizeActiveOperation();

        foreach (var (sourcePath, pageIndex) in getDirtyPages(directoryPath))
        {
            if (!tryGetPageStrokes(sourcePath, pageIndex, out var strokes))
            {
                continue;
            }

            result.AttemptedCount++;
            if (persistPage(sourcePath, pageIndex, strokes, out var errorMessage))
            {
                result.SucceededCount++;
                continue;
            }

            result.Failures.Add((sourcePath, pageIndex, errorMessage ?? "unknown-error"));
        }

        return result;
    }
}
