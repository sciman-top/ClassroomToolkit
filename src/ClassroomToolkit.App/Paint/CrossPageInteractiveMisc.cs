using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System;

namespace ClassroomToolkit.App.Paint;

internal static class BoardTransitionCrossPagePolicy
{
    internal static bool ShouldHandleCrossPageArtifacts(
        bool photoModeActive,
        bool crossPageDisplayEnabled)
    {
        // Board transitions need to clear/refresh cross-page artifacts when photo mode
        // is the active scene source, regardless of the new board active flag.
        return photoModeActive && crossPageDisplayEnabled;
    }
}

internal static class CrossPageBoundsCacheDefaults
{
    internal const int InteractiveReuseMaxAgeMs = 120;
    internal const double KeyEpsilon = 0.01;
}

internal static class CrossPageFrameSourceAssignmentPolicy
{
    internal static bool ShouldAssign(
        ImageSource? currentSource,
        ImageSource? nextSource,
        bool forceAssign = false)
    {
        if (forceAssign)
        {
            return true;
        }

        if (nextSource == null)
        {
            return currentSource != null;
        }

        return !ReferenceEquals(currentSource, nextSource);
    }
}

internal static class CrossPageOutOfPageMoveSuppressionPolicy
{
    internal static bool ShouldSuppress(
        bool crossPageDisplayActive,
        bool photoFullscreenActive,
        PaintToolMode mode,
        bool strokeInProgress,
        bool switchedPageThisFrame,
        bool recentSwitchGraceActive,
        bool hasCurrentPageRect,
        bool pointerInsideCurrentPageRect)
    {
        if (!crossPageDisplayActive
            || photoFullscreenActive
            || mode != PaintToolMode.Brush
            || !strokeInProgress
            || switchedPageThisFrame
            || recentSwitchGraceActive
            || !hasCurrentPageRect)
        {
            return false;
        }

        return !pointerInsideCurrentPageRect;
    }
}

internal static class CrossPagePendingTakeoverPolicy
{
    internal const int ImmediateTakeoverThresholdMs = 120;

    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        CrossPageDisplayUpdateDispatchDecision decision,
        CrossPageUpdateDispatchSuffix suffix,
        CrossPageDisplayUpdateRuntimeState pendingState,
        DateTime nowUtc,
        int thresholdMs = ImmediateTakeoverThresholdMs)
    {
        if (decision.Mode != CrossPageDisplayUpdateDispatchMode.SkipPending)
        {
            return decision;
        }

        if (suffix != CrossPageUpdateDispatchSuffix.Immediate)
        {
            return decision;
        }

        if (!pendingState.Pending || pendingState.PendingSinceUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return decision;
        }

        var pendingMs = (nowUtc - pendingState.PendingSinceUtc).TotalMilliseconds;
        if (pendingMs < thresholdMs)
        {
            return decision;
        }

        return new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Direct,
            DelayMs: 0);
    }
}

internal readonly record struct CrossPageRegionEraseNavigationPlan(
    bool InteractiveSwitch,
    bool DeferCrossPageDisplayUpdate);

internal static class CrossPageRegionEraseNavigationPolicy
{
    internal static CrossPageRegionEraseNavigationPlan Resolve()
    {
        return new CrossPageRegionEraseNavigationPlan(
            InteractiveSwitch: false,
            DeferCrossPageDisplayUpdate: false);
    }
}

internal static class CrossPageRegionEraseOrderPolicy
{
    internal static IReadOnlyList<int> ResolveBatchOrder(
        IEnumerable<int> pages,
        int currentPage)
    {
        if (pages == null)
        {
            return currentPage > 0 ? new[] { currentPage } : [];
        }

        var ordered = pages
            .Where(p => p > 0)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        if (currentPage <= 0)
        {
            return ordered;
        }

        ordered.Remove(currentPage);
        ordered.Add(currentPage);
        return ordered;
    }
}

internal static class CrossPageRegionErasePolicy
{
    internal static bool ShouldUseCrossPageErase(
        bool photoInkModeActive,
        bool crossPageDisplayEnabled)
    {
        return photoInkModeActive && crossPageDisplayEnabled;
    }

    internal static bool CanNavigateForRegionErase(
        bool photoInkModeActive,
        bool crossPageDisplayEnabled,
        int targetPage)
    {
        return ShouldUseCrossPageErase(photoInkModeActive, crossPageDisplayEnabled)
               && targetPage > 0;
    }
}

internal static class CrossPageViewportBoundsDefaults
{
    internal const double VisibilityMarginDip = 16.0;
    internal const double CenterRatio = 0.5;
    internal const double TranslateClampEpsilonDip = 0.5;
    internal const double ClampSlackMinDip = 32.0;
    internal const double ClampSlackViewportRatio = 0.5;
}

internal static class CrossPageViewportBoundsPolicy
{
    internal static double ResolveSlackDip(double viewportHeight)
    {
        return Math.Max(
            CrossPageViewportBoundsDefaults.ClampSlackMinDip,
            viewportHeight * CrossPageViewportBoundsDefaults.ClampSlackViewportRatio);
    }

    internal static bool IsTranslateClamped(double originalY, double clampedY)
    {
        return Math.Abs(originalY - clampedY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
    }
}

internal static class CrossPageZoomLayoutScalePolicy
{
    internal static bool ShouldSynchronize(double scaleFactor)
    {
        return double.IsFinite(scaleFactor)
            && scaleFactor > 0
            && Math.Abs(scaleFactor - 1.0) >= PhotoZoomInputDefaults.ScaleApplyEpsilon;
    }

    internal static double Scale(double value, double scaleFactor)
    {
        return value * scaleFactor;
    }
}
