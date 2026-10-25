using System.Windows;
using System;
using WpfPoint
=
System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputNavigation
{
    internal static int ResolveTargetPage(
        double pointerY,
        int currentPage,
        int totalPages,
        double currentTop,
        double currentHeight,
        Func<int, double> getPageHeight)
    {
        if (totalPages <= 1 || currentPage <= 0 || currentPage > totalPages || currentHeight <= 0)
        {
            return Math.Clamp(currentPage, 1, Math.Max(1, totalPages));
        }

        var currentBottom = currentTop + currentHeight;
        if (pointerY >= currentTop && pointerY <= currentBottom)
        {
            return currentPage;
        }

        if (pointerY < currentTop)
        {
            var top = currentTop;
            for (int page = currentPage - 1; page >= 1; page--)
            {
                var height = Math.Max(0, getPageHeight(page));
                top -= height;
                if (pointerY >= top && pointerY <= top + height)
                {
                    return page;
                }
            }
            return 1;
        }

        var nextTop = currentBottom;
        for (int page = currentPage + 1; page <= totalPages; page++)
        {
            var height = Math.Max(0, getPageHeight(page));
            if (pointerY >= nextTop && pointerY <= nextTop + height)
            {
                return page;
            }
            nextTop += height;
        }

        return totalPages;
    }

    internal static double ComputePageOffset(
        int currentPage,
        int targetPage,
        Func<int, double> getPageHeight)
    {
        if (targetPage == currentPage)
        {
            return 0;
        }

        double offset = 0;
        if (targetPage > currentPage)
        {
            for (int page = currentPage; page < targetPage; page++)
            {
                offset += Math.Max(0, getPageHeight(page));
            }
            return offset;
        }

        for (int page = currentPage - 1; page >= targetPage; page--)
        {
            offset -= Math.Max(0, getPageHeight(page));
        }
        return offset;
    }
}

internal enum CrossPageInputResumeAction
{
    None = 0,
    BeginBrushContinuation = 1,
    BeginEraser = 2
}

internal readonly record struct CrossPageInputResumeExecutionPlan(
    CrossPageInputResumeAction Action,
    bool ShouldClearPendingBrushState,
    bool ShouldUpdateBrushAfterContinuation);

internal static class CrossPageInputSwitchDefaults
{
    internal const double MinPositiveHysteresisDip = 0;
}

internal readonly record struct CrossPageInputSwitchExecutionPlan(
    bool ShouldSwitch,
    bool ShouldResolveBrushContinuation,
    bool DeferCrossPageDisplayUpdate);

internal readonly record struct CrossPageInputSwitchNavigationPlan(
    bool InteractiveSwitch,
    bool DeferCrossPageDisplayUpdate);

internal static class CrossPageInputSwitchThresholds
{
    internal const double PointerHysteresisDip = 10.0;
    internal const double OutOfPageMoveSuppressMarginDip = 2.0;
    internal const int OutOfPageMoveSuppressPostSwitchGraceMs = 120;
    internal const double ReverseSwitchSeamBandDip = 18.0;
    internal const int ReverseSwitchCooldownMs = 90;
}

internal static class CrossPageInteractiveSwitchClampDefaults
{
    internal const int MinPageIndex = 1;
    internal const double MinFallbackPageHeight = 1.0;
    internal const double MinResolvedPageHeight = 0.0;
}

internal enum CrossPageInteractiveSwitchRefreshMode
{
    DeferredByInput,
    ImmediateDirect,
    ImmediateScheduled
}

internal static class CrossPageInputSwitchPolicies
{
    internal static bool IsActive(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        return PhotoWindowPolicies.IsCrossPageDisplayActive(
            photoModeActive,
            boardActive,
            crossPageDisplayEnabled);
    }

    internal static CrossPageInputResumeExecutionPlan ResolveCrossPageInputResume(
        bool switchedPage,
        PaintToolMode mode,
        bool strokeInProgress,
        bool isErasing,
        bool replayCurrentInput,
        bool hasPendingBrushSeed,
        bool pendingSeedEqualsInput)
    {
        if (!switchedPage)
        {
            return new CrossPageInputResumeExecutionPlan(
                CrossPageInputResumeAction.None,
                ShouldClearPendingBrushState: false,
                ShouldUpdateBrushAfterContinuation: false);
        }

        if (mode == PaintToolMode.Brush && !strokeInProgress)
        {
            return new CrossPageInputResumeExecutionPlan(
                CrossPageInputResumeAction.BeginBrushContinuation,
                ShouldClearPendingBrushState: true,
                ShouldUpdateBrushAfterContinuation: replayCurrentInput || hasPendingBrushSeed || !pendingSeedEqualsInput);
        }

        if (mode == PaintToolMode.Eraser && !isErasing)
        {
            return new CrossPageInputResumeExecutionPlan(
                CrossPageInputResumeAction.BeginEraser,
                ShouldClearPendingBrushState: false,
                ShouldUpdateBrushAfterContinuation: false);
        }

        return new CrossPageInputResumeExecutionPlan(
            CrossPageInputResumeAction.None,
            ShouldClearPendingBrushState: false,
            ShouldUpdateBrushAfterContinuation: false);
    }

    internal static bool ShouldProceed(
        bool canSwitchByGate,
        bool hasBitmap,
        bool hasCurrentRect,
        bool shouldSwitchByPointer)
    {
        if (!canSwitchByGate || !hasBitmap)
        {
            return false;
        }

        return !hasCurrentRect || shouldSwitchByPointer;
    }

    internal static bool ShouldSuppress(
        int currentPage,
        int targetPage,
        int lastSwitchFromPage,
        int lastSwitchToPage,
        DateTime lastSwitchUtc,
        DateTime nowUtc,
        double pointerY,
        double seamY,
        double seamBandDip,
        int cooldownMs)
    {
        if (currentPage <= 0
            || targetPage <= 0
            || currentPage == targetPage
            || lastSwitchUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        var reverseDirection = lastSwitchFromPage == targetPage
            && lastSwitchToPage == currentPage;
        if (!reverseDirection)
        {
            return false;
        }

        var elapsedMs = (nowUtc - lastSwitchUtc).TotalMilliseconds;
        if (elapsedMs < 0 || elapsedMs > cooldownMs)
        {
            return false;
        }

        return Math.Abs(pointerY - seamY) <= seamBandDip;
    }

    internal static CrossPageInputSwitchExecutionPlan ResolveExecution(
        int currentPage,
        int targetPage,
        PaintToolMode mode,
        double currentPageHeight)
    {
        if (targetPage == currentPage || targetPage <= 0)
        {
            return new CrossPageInputSwitchExecutionPlan(
                ShouldSwitch: false,
                ShouldResolveBrushContinuation: false,
                DeferCrossPageDisplayUpdate: false);
        }

        var shouldResolveBrushContinuation = mode == PaintToolMode.Brush && currentPageHeight > 0;
        var deferCrossPageDisplayUpdate = mode != PaintToolMode.Brush;
        return new CrossPageInputSwitchExecutionPlan(
            ShouldSwitch: true,
            ShouldResolveBrushContinuation: shouldResolveBrushContinuation,
            DeferCrossPageDisplayUpdate: deferCrossPageDisplayUpdate);
    }

    internal static bool CanSwitchForInput(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool boardActive,
        PaintToolMode mode,
        bool photoPanning,
        bool crossPageDragging)
    {
        if (!photoModeActive || !crossPageDisplayEnabled || boardActive)
        {
            return false;
        }
        if (mode != PaintToolMode.Brush && mode != PaintToolMode.Eraser)
        {
            return false;
        }

        // Avoid competing state updates between drag/pan and cross-page ink routing.
        return !photoPanning && !crossPageDragging;
    }

    internal static CrossPageInputSwitchNavigationPlan ResolveNavigation(
        PaintToolMode mode,
        bool strokeInProgress,
        bool isErasing,
        bool isRegionSelecting,
        bool inputTriggeredByActiveInkMutation = false)
    {
        var hasActiveInkMutation = inputTriggeredByActiveInkMutation
            || strokeInProgress
            || isErasing
            || isRegionSelecting;
        if (hasActiveInkMutation
            && (mode == PaintToolMode.Brush
                || mode == PaintToolMode.Eraser
                || mode == PaintToolMode.RegionErase))
        {
            // Brush seam continuation needs the interactive path so the current page and
            // its neighbor frames stay coherent while the stroke crosses the seam.
            if (mode == PaintToolMode.Brush)
            {
                return new CrossPageInputSwitchNavigationPlan(
                    InteractiveSwitch: true,
                    DeferCrossPageDisplayUpdate: true);
            }

            // Eraser and region erase still prefer the stable path because their geometry
            // mutations are not resumed as a continuous stroke across the seam.
            return new CrossPageInputSwitchNavigationPlan(
                InteractiveSwitch: false,
                DeferCrossPageDisplayUpdate: false);
        }

        return new CrossPageInputSwitchNavigationPlan(
            InteractiveSwitch: true,
            DeferCrossPageDisplayUpdate: true);
    }

    internal static bool ShouldSwitchByPointer(
        Rect currentPageRect,
        WpfPoint pointer,
        double hysteresisDip)
    {
        if (currentPageRect.IsEmpty || hysteresisDip <= CrossPageInputSwitchDefaults.MinPositiveHysteresisDip)
        {
            return !currentPageRect.Contains(pointer);
        }

        var expanded = currentPageRect;
        expanded.Inflate(hysteresisDip, hysteresisDip);
        return !expanded.Contains(pointer);
    }

    internal static bool ShouldSwitchForInput(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool boardActive,
        PaintToolMode mode,
        bool photoPanning,
        bool crossPageDragging,
        bool hasBitmap,
        Rect? currentPageRect,
        WpfPoint pointer,
        double pointerHysteresisDip)
    {
        var canSwitchByGate = CrossPageInputSwitchPolicies.CanSwitchForInput(
            photoModeActive,
            crossPageDisplayEnabled,
            boardActive,
            mode,
            photoPanning,
            crossPageDragging);
        var hasCurrentRect = currentPageRect.HasValue;
        var shouldSwitchByPointer = true;
        if (hasCurrentRect && currentPageRect is Rect resolvedRect)
        {
            shouldSwitchByPointer = CrossPageInputSwitchPolicies.ShouldSwitchByPointer(
                resolvedRect,
                pointer,
                pointerHysteresisDip);
        }
        return CrossPageInputSwitchPolicies.ShouldProceed(
            canSwitchByGate,
            hasBitmap,
            hasCurrentRect,
            shouldSwitchByPointer);
    }

    internal static int ResolveNeighborTargetPage(
        int currentPage,
        int requestedPage)
    {
        if (requestedPage == currentPage)
        {
            return currentPage;
        }

        if (requestedPage > currentPage)
        {
            return currentPage + 1;
        }

        return currentPage - 1;
    }

    internal static double ClampTranslateY(
        double candidateTranslateY,
        int targetPage,
        int totalPages,
        double viewportHeight,
        Func<int, double> getPageHeight,
        double fallbackPageHeight)
    {
        if (targetPage <= 0 || totalPages <= 0 || viewportHeight <= 0)
        {
            return candidateTranslateY;
        }

        var boundedTargetPage = Math.Clamp(
            targetPage,
            CrossPageInteractiveSwitchClampDefaults.MinPageIndex,
            totalPages);
        var safeFallbackHeight = Math.Max(
            CrossPageInteractiveSwitchClampDefaults.MinFallbackPageHeight,
            fallbackPageHeight);
        var targetHeight = ResolveHeight(boundedTargetPage, getPageHeight, safeFallbackHeight);

        double totalHeightAbove = 0;
        for (int page = CrossPageInteractiveSwitchClampDefaults.MinPageIndex; page < boundedTargetPage; page++)
        {
            totalHeightAbove += ResolveHeight(page, getPageHeight, safeFallbackHeight);
        }

        double totalHeightBelow = 0;
        for (int page = boundedTargetPage + 1; page <= totalPages; page++)
        {
            totalHeightBelow += ResolveHeight(page, getPageHeight, safeFallbackHeight);
        }

        var maxY = totalHeightAbove;
        var minY = -(targetHeight + totalHeightBelow - viewportHeight);
        if (minY > maxY)
        {
            var center = (minY + maxY) * 0.5;
            minY = center;
            maxY = center;
        }

        return Math.Clamp(candidateTranslateY, minY, maxY);
    }

    private static double ResolveHeight(
        int pageIndex,
        Func<int, double> getPageHeight,
        double fallbackPageHeight)
    {
        var height = Math.Max(CrossPageInteractiveSwitchClampDefaults.MinResolvedPageHeight, getPageHeight(pageIndex));
        return height > CrossPageInteractiveSwitchClampDefaults.MinResolvedPageHeight
            ? height
            : fallbackPageHeight;
    }

    internal static CrossPageInteractiveSwitchRefreshMode ResolveCrossPageInteractiveSwitchRefresh(
        PaintToolMode mode,
        bool deferCrossPageDisplayUpdate)
    {
        if (deferCrossPageDisplayUpdate)
        {
            return CrossPageInteractiveSwitchRefreshMode.DeferredByInput;
        }

        return mode == PaintToolMode.Brush
            ? CrossPageInteractiveSwitchRefreshMode.ImmediateDirect
            : CrossPageInteractiveSwitchRefreshMode.ImmediateScheduled;
    }
}
