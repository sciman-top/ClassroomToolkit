using ClassroomToolkit.App.Windowing;
using System.Windows;
using System;

namespace ClassroomToolkit.App;

internal static class LauncherWindowRuntimeSelectionLogPolicy
{
    internal static bool ShouldLog(LauncherWindowRuntimeSelectionReason reason)
    {
        return reason == LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible
               || reason == LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible;
    }
}

internal static class LauncherWindowResolutionPolicy
{
    internal static bool ShouldUseBubbleWindow(
        LauncherWindowKind resolvedKind,
        bool bubbleWindowExists)
    {
        return resolvedKind == LauncherWindowKind.Bubble && bubbleWindowExists;
    }
}

internal static class LauncherTopmostVisibilityTimestampPolicy
{
    internal static DateTime ResolveLastVisibleUtc(
        DateTime previousUtc,
        DateTime nowUtc,
        bool visibleForTopmost)
    {
        return visibleForTopmost ? nowUtc : previousUtc;
    }
}

internal static class LauncherTopmostVisibilityStateUpdater
{
    internal static void ApplyResolvedTimestamp(
        ref DateTime lastVisibleUtc,
        DateTime nowUtc,
        bool visibleForTopmost)
    {
        lastVisibleUtc = LauncherTopmostVisibilityTimestampPolicy.ResolveLastVisibleUtc(
            lastVisibleUtc,
            nowUtc,
            visibleForTopmost);
    }
}

internal readonly record struct LauncherAutoExitTimerPlan(
    bool ShouldStart,
    TimeSpan Interval);

internal static class LauncherAutoExitTimerPlanPolicy
{
    internal static LauncherAutoExitTimerPlan Resolve(int autoExitSeconds)
    {
        if (autoExitSeconds <= 0)
        {
            return new LauncherAutoExitTimerPlan(
                ShouldStart: false,
                Interval: TimeSpan.Zero);
        }

        return new LauncherAutoExitTimerPlan(
            ShouldStart: true,
            Interval: TimeSpan.FromSeconds(autoExitSeconds));
    }
}

internal static class LauncherWorkAreaClampPolicy
{
    internal static System.Windows.Point Resolve(
        double left,
        double top,
        double width,
        double height,
        Rect workArea)
    {
        var resolvedLeft = left;
        var resolvedTop = top;

        if (resolvedLeft < workArea.Left)
        {
            resolvedLeft = workArea.Left;
        }

        if (resolvedTop < workArea.Top)
        {
            resolvedTop = workArea.Top;
        }

        if (resolvedLeft + width > workArea.Right)
        {
            resolvedLeft = workArea.Right - width;
        }

        if (resolvedTop + height > workArea.Bottom)
        {
            resolvedTop = workArea.Bottom - height;
        }

        return new System.Windows.Point(resolvedLeft, resolvedTop);
    }
}

internal static class RollCallRuntimeDefaults
{
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
    internal const int ClassSwitchSuppressMs = 250;
}
