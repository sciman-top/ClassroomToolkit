using System;

namespace ClassroomToolkit.App;

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
