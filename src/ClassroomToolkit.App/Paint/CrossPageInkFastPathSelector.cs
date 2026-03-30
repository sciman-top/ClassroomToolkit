using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInkFastPathSelector
{
    internal readonly record struct CrossPageInkFastPathDecision(bool ShouldApply, string Reason);

    internal static bool ShouldUseNeighborBitmapFastPath(
        bool interactiveSwitch,
        IReadOnlyList<Ink.InkStrokeData>? currentPageStrokes,
        IReadOnlyList<Ink.InkStrokeData>? neighborCacheStrokes)
    {
        if (!interactiveSwitch || currentPageStrokes == null || neighborCacheStrokes == null)
        {
            return false;
        }

        if (currentPageStrokes.Count == 0)
        {
            return false;
        }

        return ReferenceEquals(currentPageStrokes, neighborCacheStrokes);
    }

    internal static CrossPageInkFastPathDecision EvaluateCandidateForRasterCopy(
        bool interactiveSwitch,
        IReadOnlyList<Ink.InkStrokeData>? currentPageStrokes,
        IReadOnlyList<Ink.InkStrokeData>? candidateStrokes,
        int candidatePixelWidth,
        int candidatePixelHeight,
        double candidateDpiX,
        double candidateDpiY,
        int surfacePixelWidth,
        int surfacePixelHeight,
        double surfaceDpiX,
        double surfaceDpiY)
    {
        if (!interactiveSwitch)
        {
            return new CrossPageInkFastPathDecision(false, "not-interactive-switch");
        }

        if (currentPageStrokes == null || candidateStrokes == null)
        {
            return new CrossPageInkFastPathDecision(false, "strokes-missing");
        }

        if (currentPageStrokes.Count == 0)
        {
            return new CrossPageInkFastPathDecision(false, "strokes-empty");
        }

        if (!ReferenceEquals(currentPageStrokes, candidateStrokes))
        {
            return new CrossPageInkFastPathDecision(false, "stroke-reference-mismatch");
        }

        if (candidatePixelWidth <= 0 || candidatePixelHeight <= 0)
        {
            return new CrossPageInkFastPathDecision(false, "bitmap-invalid");
        }

        if (surfacePixelWidth <= 0 || surfacePixelHeight <= 0)
        {
            return new CrossPageInkFastPathDecision(false, "surface-invalid");
        }

        if (candidatePixelWidth != surfacePixelWidth || candidatePixelHeight != surfacePixelHeight)
        {
            return new CrossPageInkFastPathDecision(false, "size-mismatch");
        }

        if (Math.Abs(candidateDpiX - surfaceDpiX) > 0.5 || Math.Abs(candidateDpiY - surfaceDpiY) > 0.5)
        {
            return new CrossPageInkFastPathDecision(false, "dpi-mismatch");
        }

        return new CrossPageInkFastPathDecision(true, "ok");
    }
}
