using System.Threading;
using System;

namespace ClassroomToolkit.App;

internal readonly record struct InkStartupCleanupSummary(
    int TotalSidecars,
    int TotalComposites);

internal sealed class ApplicationExitCoordinator
{
    private bool _inProgress;

    internal bool TryExit(
        Func<bool> saveSettings,
        Func<bool> confirmDiscardSettings,
        Func<bool, bool> prepareChildWindows,
        Action shutdown)
    {
        // Save-error dialogs pump the dispatcher; a second exit must not begin
        // cancelling resources while the first request still awaits a decision.
        if (_inProgress)
        {
            return false;
        }

        _inProgress = true;
        try
        {
            var discardSettings = !saveSettings();
            if (discardSettings && !confirmDiscardSettings())
            {
                return false;
            }
            if (!prepareChildWindows(discardSettings))
            {
                return false;
            }

            shutdown();
            return true;
        }
        finally
        {
            _inProgress = false;
        }
    }
}

internal static class NotificationAndExitPolicies
{
    internal static bool IsNotified(ref int notifiedState)
    {
        return Volatile.Read(ref notifiedState) != 0;
    }

    internal static bool TryClaimNotification(ref int notifiedState)
    {
        return Interlocked.Exchange(ref notifiedState, 1) == 0;
    }

    internal static void Reset(ref int notifiedState)
    {
        Interlocked.Exchange(ref notifiedState, 0);
    }

    internal static bool ShouldLogDeletionSummary(InkStartupCleanupSummary summary)
    {
        return summary.TotalSidecars > 0 || summary.TotalComposites > 0;
    }

    internal static string FormatDeletionSummary(InkStartupCleanupSummary summary)
    {
        return $"[InkStartupCleanup] deleted orphan sidecars={summary.TotalSidecars}, composites={summary.TotalComposites}";
    }

    internal static string FormatFailureMessage(string message)
    {
        return $"[InkStartupCleanup] failed: {message}";
    }

    internal static DateTime ExtendSuppressUntil(
        DateTime currentSuppressUntilUtc,
        DateTime nowUtc,
        TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return currentSuppressUntilUtc;
        }

        var nextSuppressUntilUtc = nowUtc.Add(duration);
        return nextSuppressUntilUtc > currentSuppressUntilUtc
            ? nextSuppressUntilUtc
            : currentSuppressUntilUtc;
    }

    internal static bool ShouldSuppress(
        DateTime suppressUntilUtc,
        DateTime nowUtc)
    {
        return suppressUntilUtc >= nowUtc;
    }
}
