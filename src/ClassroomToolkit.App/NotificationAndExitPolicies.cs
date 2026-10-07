using System.Threading;
using System;

namespace ClassroomToolkit.App;

internal static class SpeechUnavailableNotificationPolicy
{
    internal static bool ShouldNotify(ref int notifiedState)
    {
        return Interlocked.Exchange(ref notifiedState, 1) == 0;
    }
}

internal static class RemoteHookUnavailableNotificationPolicy
{
    internal static bool IsNotified(ref int notifiedState)
    {
        return Volatile.Read(ref notifiedState) != 0;
    }

    internal static bool ShouldNotify(ref int notifiedState)
    {
        return Interlocked.Exchange(ref notifiedState, 1) == 0;
    }

    internal static void Reset(ref int notifiedState)
    {
        Interlocked.Exchange(ref notifiedState, 0);
    }
}

internal static class SettingsSaveFailureNotificationStateUpdater
{
    internal static void MarkSaveSucceeded(ref bool saveFailedNotified)
    {
        saveFailedNotified = false;
    }

    internal static void ApplyNotificationPlan(
        ref bool saveFailedNotified,
        SettingsSaveFailureNotificationPlan plan)
    {
        saveFailedNotified = plan.NextNotifiedState;
    }
}

internal readonly record struct SettingsSaveFailureNotificationPlan(
    bool ShouldNotify,
    bool NextNotifiedState);

internal static class SettingsSaveFailureNotificationPolicy
{
    internal static SettingsSaveFailureNotificationPlan Resolve(bool alreadyNotified)
    {
        if (alreadyNotified)
        {
            return new SettingsSaveFailureNotificationPlan(
                ShouldNotify: false,
                NextNotifiedState: true);
        }

        return new SettingsSaveFailureNotificationPlan(
            ShouldNotify: true,
            NextNotifiedState: true);
    }
}

internal static class DialogShowResultStateUpdater
{
    internal static void MarkFromDialogResult(
        ref bool result,
        bool? dialogResult)
    {
        result = dialogResult == true;
    }
}

internal readonly record struct InkStartupCleanupSummary(
    int TotalSidecars,
    int TotalComposites);

internal static class InkStartupCleanupLogPolicy
{
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
}

internal static class RollCallClickSuppressionPolicy
{
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
