using System.Collections.Generic;
using System.Windows;
using System;

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

internal static class CrossPageInteractiveHoldDurationDefaults
{
    internal const int BaseMs = 220;
    internal const int ExtraPerNeighborMs = 40;
    internal const int MaxMs = 380;
    internal const int BrushModeExtraMs = 80;
    internal const int EraserModeExtraMs = 40;
}

internal enum CrossPageInteractiveInkSlotRemapAction
{
    KeepCurrentFrame = 0,
    UsePreservedFrame = 1,
    ClearCurrentFrame = 2
}

internal readonly record struct CrossPageNeighborInkFrameDecision(
    bool ClearCurrentFrame,
    bool AllowResolvedInkReplacement,
    bool KeepVisible);

internal readonly record struct CrossPageNeighborInkRenderSurfacePlan(
    int PixelWidth,
    int PixelHeight,
    double HorizontalOffsetDip);

internal readonly record struct CrossPageNeighborPageFrameDecision(
    bool HoldCurrentFrame,
    bool CollapseSlot);

internal static class CrossPageNeighborPagesClearDefaults
{
    internal const int MinGraceMs = 0;
}

internal static class CrossPageNeighborPrefetchDefaults
{
    internal const int RadiusDefault = 2;
    internal const int RadiusMin = 1;
    internal const int RadiusMax = 4;
    internal const int NeighborInkCacheLimit = 10;
}

internal static class CrossPageSwitchBitmapResolver
{
    internal static TBitmap? ResolveForInteractiveSwitch<TBitmap>(
        bool interactiveSwitch,
        TBitmap? preloadedBitmap,
        Func<TBitmap?> loadBitmap)
        where TBitmap : class
    {
        if (interactiveSwitch && preloadedBitmap != null)
        {
            return preloadedBitmap;
        }

        return loadBitmap();
    }
}

internal static class CrossPageNeighborInkPolicies
{
    internal static int ResolveMs(
        int visibleNeighborPages,
        PaintToolMode mode = PaintToolMode.Cursor,
        int baseMs = CrossPageInteractiveHoldDurationDefaults.BaseMs,
        int extraPerNeighborMs = CrossPageInteractiveHoldDurationDefaults.ExtraPerNeighborMs,
        int maxMs = CrossPageInteractiveHoldDurationDefaults.MaxMs)
    {
        if (mode == PaintToolMode.Cursor)
        {
            return 0;
        }

        var normalizedVisible = Math.Max(0, visibleNeighborPages);
        var extraNeighbors = Math.Max(0, normalizedVisible - 1);
        var modeExtraMs = mode switch
        {
            PaintToolMode.Brush => CrossPageInteractiveHoldDurationDefaults.BrushModeExtraMs,
            PaintToolMode.Eraser => CrossPageInteractiveHoldDurationDefaults.EraserModeExtraMs,
            _ => 0
        };
        var value = baseMs + (extraNeighbors * Math.Max(0, extraPerNeighborMs)) + modeExtraMs;
        if (maxMs <= 0)
        {
            return Math.Max(1, value);
        }

        return Math.Clamp(value, 1, maxMs);
    }

    internal static bool ShouldClearCurrentFrame(
        bool holdInkReplacement,
        bool hasNeighborInkStrokes,
        bool inkOperationActive,
        bool interactionActive)
    {
        return !holdInkReplacement
            && !interactionActive
            && !inkOperationActive
            && !hasNeighborInkStrokes;
    }

    internal static bool ShouldHoldReplacement(
        int pageIndex,
        int pinnedNeighborPage,
        DateTime holdUntilUtc,
        DateTime nowUtc,
        bool hasCurrentInkFrame)
    {
        if (!hasCurrentInkFrame)
        {
            return false;
        }
        if (pinnedNeighborPage <= 0 || pageIndex != pinnedNeighborPage)
        {
            return false;
        }
        if (holdUntilUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        return nowUtc <= holdUntilUtc;
    }

    internal static bool ShouldReplaceCrossPageInteractiveInkReplacement(
        bool hasResolvedInkBitmap,
        bool holdInkReplacement,
        bool hasCurrentInkFrame,
        bool slotPageChanged)
    {
        if (!hasResolvedInkBitmap)
        {
            return false;
        }

        // Slot changed means target page changed; keeping previous page ink causes
        // visual duplication across pages. Always switch to resolved target frame.
        if (slotPageChanged)
        {
            return true;
        }

        // For same-slot updates, allow replacement unless hold explicitly blocks it.
        return !holdInkReplacement || !hasCurrentInkFrame;
    }

    internal static CrossPageInteractiveInkSlotRemapAction ResolveCrossPageInteractiveInkSlotRemap(
        bool slotPageChanged,
        bool hasResolvedInkBitmap,
        bool hasCurrentInkFrame,
        bool hasPreservedInkFrame,
        bool inkOperationActive)
    {
        if (!slotPageChanged || hasResolvedInkBitmap)
        {
            return CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame;
        }

        // During active ink mutation, prefer preserved frame for the target page when available.
        // This avoids clear-then-fill flashes on seam crossing while keeping ownership by page uid.
        if (inkOperationActive)
        {
            if (hasPreservedInkFrame)
            {
                return CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame;
            }

            // Slot remapped without a preserved target frame: keeping current frame would
            // display old-page ink on the new slot (flash + jitter during seam crossing).
            return hasCurrentInkFrame
                ? CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame
                : CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame;
        }

        if (hasPreservedInkFrame)
        {
            return CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame;
        }

        return hasCurrentInkFrame
            ? CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame
            : CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame;
    }

    internal static bool ResolveCrossPageInteractiveNeighborInkHold(
        bool baseHoldReplacement,
        bool interactionActive,
        bool hasCurrentInkFrame,
        bool inkOperationActive,
        bool slotPageChanged)
    {
        // Slot remap means this slot now points to another page. Holding current frame here
        // keeps old-page ink alive on the wrong slot and manifests as seam flash/jitter.
        if (slotPageChanged)
        {
            return false;
        }

        if (baseHoldReplacement)
        {
            return true;
        }

        // Keep current neighbor ink only while interactive ink mutation is in progress.
        // Plain cursor pan should not hold stale frames, otherwise old-page ink can
        // temporarily ride on top of a remapped neighbor page.
        return interactionActive && inkOperationActive && hasCurrentInkFrame;
    }

    internal static bool ShouldReplaceCrossPageInteractivePageReplacement(
        bool hasResolvedTargetFrame,
        bool interactionActive,
        bool slotPageChanged,
        bool hasCurrentFrame)
    {
        if (!hasResolvedTargetFrame)
        {
            return false;
        }

        // During interaction, keep same-page slot stable and move by transform only.
        // This avoids replacing with a different bitmap instance and reduces flash.
        if (interactionActive && !slotPageChanged && hasCurrentFrame)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldReuseCurrentFrame(
        bool shouldReplacePageFrame,
        bool slotPageChanged,
        bool hasCurrentFrame)
    {
        if (shouldReplacePageFrame || !hasCurrentFrame)
        {
            return false;
        }

        // Slot remap means this visual slot now points to another page.
        // Reusing old frame here causes cross-page ghost duplication.
        return !slotPageChanged;
    }

    internal static bool ShouldReleasePin(
        DateTime holdUntilUtc,
        DateTime nowUtc,
        bool interactionActive)
    {
        if (holdUntilUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        if (interactionActive)
        {
            return false;
        }

        return nowUtc > holdUntilUtc;
    }

    internal static bool ShouldReplaceFrame(
        bool inkShowEnabled,
        bool hasCurrentFrame,
        bool hasResolvedTargetFrame,
        bool slotPageChanged)
    {
        if (!inkShowEnabled)
        {
            return true;
        }

        if (hasResolvedTargetFrame)
        {
            return true;
        }

        if (slotPageChanged)
        {
            // Slot remapped to another page and target ink frame is still unresolved:
            // clear stale old-page frame to avoid one-frame flash on seam crossing.
            return true;
        }

        // Keep current frame when target bitmap is temporarily unavailable.
        return !hasCurrentFrame;
    }

    internal static bool ShouldClearPreservedNeighborInkFrames(
        bool pageChanged,
        bool interactiveSwitch,
        bool inputTriggeredByActiveInkMutation,
        PaintToolMode mode)
    {
        if (!pageChanged || interactiveSwitch || !inputTriggeredByActiveInkMutation)
        {
            return false;
        }

        return mode == PaintToolMode.Brush;
    }

    internal static int ResolvePreservedPage(
        bool clearPreservedNeighborInkFrames,
        bool pageChanged,
        int previousPage,
        int currentPage)
    {
        if (!clearPreservedNeighborInkFrames || !pageChanged)
        {
            return 0;
        }

        if (previousPage <= 0 || previousPage == currentPage)
        {
            return 0;
        }

        return previousPage;
    }

    internal static bool ShouldSeedPreviousPageAfterClear(
        bool clearPreservedNeighborInkFrames,
        bool pageChanged,
        int previousPage,
        int currentPage)
    {
        if (!clearPreservedNeighborInkFrames || !pageChanged)
        {
            return false;
        }

        if (previousPage <= 0 || previousPage == currentPage)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldAllowSynchronousResolveCrossPageNeighborBitmapResolve(
        bool interactionActive,
        bool slotPageChanged)
    {
        if (!interactionActive)
        {
            return true;
        }

        // During active interaction, only allow sync resolve for unchanged slots.
        // Slot remap should stay async to avoid decode/render spikes and ghost flashes.
        return !slotPageChanged;
    }

    internal static bool ShouldAllowSynchronousResolveCrossPageNeighborHeightResolve(
        bool interactionActive,
        bool photoDocumentIsPdf)
    {
        if (!interactionActive)
        {
            return true;
        }

        // PDF page-size probing is cheap and keeps seam bounds stable during zoom/pan.
        // Keep image sequence interaction on async path to avoid decode stalls.
        return photoDocumentIsPdf;
    }

    internal static CrossPageNeighborInkFrameDecision ResolveFrame(
        bool slotPageChanged,
        bool hasCurrentInkFrame,
        bool hasTargetInkStrokes,
        bool holdInkReplacement,
        bool usedPreservedInkFrame,
        bool hasResolvedInkBitmap)
    {
        if (!hasTargetInkStrokes && !hasResolvedInkBitmap)
        {
            var retainCurrentFrame = holdInkReplacement
                || usedPreservedInkFrame
                || (hasCurrentInkFrame && !slotPageChanged);
            return new CrossPageNeighborInkFrameDecision(
                ClearCurrentFrame: !retainCurrentFrame,
                AllowResolvedInkReplacement: false,
                KeepVisible: retainCurrentFrame);
        }

        var keepExistingFrame = CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
            slotPageChanged,
            hasCurrentInkFrame);
        var preservePreservedUntilReplacement = usedPreservedInkFrame && !hasResolvedInkBitmap;
        var clearCurrentFrame = !keepExistingFrame
            && !holdInkReplacement
            && !preservePreservedUntilReplacement;
        var allowResolvedInkReplacement = hasResolvedInkBitmap
            && !holdInkReplacement
            && CrossPageNeighborInkPolicies.ShouldReplaceReplacement(
                slotPageChanged,
                hasCurrentInkFrame,
                usedPreservedInkFrame);
        var keepVisible = keepExistingFrame
            || preservePreservedUntilReplacement
            || allowResolvedInkReplacement
            || holdInkReplacement;

        return new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: clearCurrentFrame,
            AllowResolvedInkReplacement: allowResolvedInkReplacement,
            KeepVisible: keepVisible);
    }

    internal static bool ShouldClearWhenUnresolved(
        CrossPageNeighborInkFrameDecision decision,
        bool hasResolvedInkBitmap)
    {
        return decision.ClearCurrentFrame && !hasResolvedInkBitmap;
    }

    internal static bool ShouldKeepExistingInkFrame(
        bool slotPageChanged,
        bool hasExistingInkFrame)
    {
        if (slotPageChanged || !hasExistingInkFrame)
        {
            return false;
        }

        // Keep previous frame for the same page until replacement is ready,
        // regardless of interaction state, to avoid one-frame flash.
        return true;
    }

    internal static bool ShouldRejectStaleCacheKey(string cacheKey, string expectedCacheKey)
    {
        if (string.IsNullOrWhiteSpace(expectedCacheKey))
        {
            return true;
        }

        return !string.Equals(cacheKey, expectedCacheKey, System.StringComparison.Ordinal);
    }

    // Keep horizontal overflow bounded to avoid large RenderTargetBitmap allocations
    // when malformed stroke geometry reports extreme bounds.
    internal const int MaxHorizontalOverflowDipPerSidePx = 320;

    internal static CrossPageNeighborInkRenderSurfacePlan ResolveRenderSurface(
        int pagePixelWidth,
        int pagePixelHeight,
        double dpiX,
        double pageWidthDip,
        double minStrokeXDip,
        double maxStrokeXDip)
    {
        if (pagePixelWidth <= 0 || pagePixelHeight <= 0 || pageWidthDip <= 0)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        if (double.IsNaN(minStrokeXDip)
            || double.IsNaN(maxStrokeXDip)
            || double.IsInfinity(minStrokeXDip)
            || double.IsInfinity(maxStrokeXDip)
            || maxStrokeXDip <= minStrokeXDip)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        var safeDpiX = dpiX > 1 ? dpiX : 96.0;
        var leftOverflowDip = Math.Max(0, -minStrokeXDip);
        var rightOverflowDip = Math.Max(0, maxStrokeXDip - pageWidthDip);

        if (leftOverflowDip <= 0.01 && rightOverflowDip <= 0.01)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        var maxOverflowDip = MaxHorizontalOverflowDipPerSidePx;
        leftOverflowDip = Math.Min(leftOverflowDip, maxOverflowDip);
        rightOverflowDip = Math.Min(rightOverflowDip, maxOverflowDip);

        var leftOverflowPx = (int)Math.Ceiling(leftOverflowDip * safeDpiX / 96.0);
        var rightOverflowPx = (int)Math.Ceiling(rightOverflowDip * safeDpiX / 96.0);

        if (leftOverflowPx <= 0 && rightOverflowPx <= 0)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        var resolvedWidth = checked(pagePixelWidth + leftOverflowPx + rightOverflowPx);
        var resolvedOffsetDip = leftOverflowPx * 96.0 / safeDpiX;
        return new CrossPageNeighborInkRenderSurfacePlan(resolvedWidth, pagePixelHeight, resolvedOffsetDip);
    }

    internal static bool ShouldReplaceReplacement(
        bool slotPageChanged,
        bool hasCurrentInkFrame,
        bool usedPreservedInkFrame)
    {
        if (!hasCurrentInkFrame)
        {
            return true;
        }

        if (!slotPageChanged)
        {
            return false;
        }

        // During slot remap, keep preserved frame until a truly newer render arrives.
        return !usedPreservedInkFrame;
    }

    internal static bool ShouldUseCandidate(
        Visibility visibility,
        bool hasBitmap,
        bool hasCandidatePage,
        int candidatePage,
        int currentPage,
        bool hasRect,
        bool pointerInsideRect)
    {
        if (visibility != Visibility.Visible || !hasBitmap || !hasCandidatePage)
        {
            return false;
        }
        if (Math.Abs(candidatePage - currentPage) > 1 || !hasRect)
        {
            return false;
        }
        return pointerInsideRect;
    }

    internal static List<(int PageIndex, double Top)> ResolveCrossPageNeighborPageDedup(
        List<(int PageIndex, double Top)> neighborPages)
    {
        if (neighborPages.Count <= 1)
        {
            return neighborPages;
        }

        var result = new List<(int PageIndex, double Top)>(neighborPages.Count);
        var seen = new HashSet<int>();
        foreach (var item in neighborPages)
        {
            if (!seen.Add(item.PageIndex))
            {
                continue;
            }
            result.Add(item);
        }
        return result;
    }

    internal static CrossPageNeighborPageFrameDecision ResolveCrossPageNeighborPageFrame(
        bool slotPageChanged,
        bool hasCurrentFrame,
        bool hasResolvedTargetFrame,
        bool interactionActive = false,
        bool preferHoldCurrentFrameOnSlotRemap = false)
    {
        if (hasResolvedTargetFrame)
        {
            return new CrossPageNeighborPageFrameDecision(
                HoldCurrentFrame: false,
                CollapseSlot: false);
        }

        if (hasCurrentFrame && interactionActive)
        {
            // Keep same-page slot stable during active interaction to avoid transient flicker.
            // During zoom-out seam expansion, temporarily holding the current slot frame also
            // avoids blank flashes when the remapped target page bitmap is still prefetching.
            // Callers must opt-in for slot remap continuity explicitly.
            if (!slotPageChanged || preferHoldCurrentFrameOnSlotRemap)
            {
                return new CrossPageNeighborPageFrameDecision(
                    HoldCurrentFrame: true,
                    CollapseSlot: false);
            }
        }

        return new CrossPageNeighborPageFrameDecision(
            HoldCurrentFrame: false,
            CollapseSlot: true);
    }

    internal static bool ShouldKeepFrames(
        bool hasVisibleNeighborFrame,
        bool interactionActive,
        DateTime lastNonEmptyUtc,
        DateTime nowUtc,
        int clearGraceMs)
    {
        if (!hasVisibleNeighborFrame)
        {
            return false;
        }

        if (interactionActive)
        {
            return true;
        }

        if (lastNonEmptyUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc
            || clearGraceMs <= CrossPageNeighborPagesClearDefaults.MinGraceMs)
        {
            return false;
        }

        return (nowUtc - lastNonEmptyUtc).TotalMilliseconds < clearGraceMs;
    }

    internal static bool ShouldSchedule(
        bool photoModeActive,
        bool photoDocumentIsPdf,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        if (!photoModeActive || photoDocumentIsPdf)
        {
            return false;
        }

        if (!crossPageDisplayEnabled && interactionActive)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldRunPrefetch(
        bool photoModeActive,
        bool photoDocumentIsPdf,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        return photoModeActive
            && !photoDocumentIsPdf
            && crossPageDisplayEnabled
            && !interactionActive;
    }
}
