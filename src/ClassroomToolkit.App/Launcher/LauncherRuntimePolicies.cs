using ClassroomToolkit.App.Windowing;
using System.Windows;
using System;

namespace ClassroomToolkit.App.Launcher;

internal readonly record struct LauncherAutoExitTimerPlan(
    bool ShouldStart,
    TimeSpan Interval);

internal static class LauncherRuntimePolicies
{
    internal static bool ShouldLog(LauncherWindowRuntimeSelectionReason reason)
    {
        return reason == LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible
               || reason == LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible;
    }

    internal static bool ShouldUseBubbleWindow(
        LauncherWindowKind resolvedKind,
        bool bubbleWindowExists)
    {
        return resolvedKind == LauncherWindowKind.Bubble && bubbleWindowExists;
    }

    internal static LauncherAutoExitTimerPlan ResolveLauncherAutoExitTimerPlan(int autoExitSeconds)
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

    internal static System.Windows.Point ResolveLauncherWorkAreaClamp(
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
