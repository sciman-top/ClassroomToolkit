using System;

namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionActivationSuppressionReason
{
    None = 0,
    NonActivatedTrigger = 1,
    PreviewTimestampUnset = 2,
    OutsideSuppressionWindow = 3,
    LauncherOnlyDrift = 4,
    PreviewAlreadyRetouched = 5
}

internal readonly record struct ToolbarInteractionActivationSuppressionDecision(
    bool ShouldSuppress,
    ToolbarInteractionActivationSuppressionReason Reason);

internal static class ToolbarInteractionActivationSuppressionPolicy
{
    internal static ToolbarInteractionActivationSuppressionDecision Resolve(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchSnapshot snapshot,
        DateTime lastPreviewMouseDownUtc,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int launcherOnlySuppressionMs = ToolbarInteractionActivationSuppressionDefaults.LauncherOnlyAfterPreviewSuppressionMs)
    {
        if (trigger != ToolbarInteractionRetouchTrigger.Activated)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: ToolbarInteractionActivationSuppressionReason.NonActivatedTrigger);
        }

        if (lastPreviewMouseDownUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: ToolbarInteractionActivationSuppressionReason.PreviewTimestampUnset);
        }

        var elapsedMs = (nowUtc - lastPreviewMouseDownUtc).TotalMilliseconds;
        if (elapsedMs < 0 || elapsedMs > launcherOnlySuppressionMs)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: ToolbarInteractionActivationSuppressionReason.OutsideSuppressionWindow);
        }

        var retouchElapsedMs = (nowUtc - lastRetouchUtc).TotalMilliseconds;
        var previewAlreadyRetouched = lastRetouchUtc != WindowDedupDefaults.UnsetTimestampUtc
                                      && lastRetouchUtc >= lastPreviewMouseDownUtc
                                      && retouchElapsedMs >= 0
                                      && retouchElapsedMs <= launcherOnlySuppressionMs;
        if (previewAlreadyRetouched)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: true,
                Reason: ToolbarInteractionActivationSuppressionReason.PreviewAlreadyRetouched);
        }

        return new ToolbarInteractionActivationSuppressionDecision(
            ShouldSuppress: false,
            Reason: ToolbarInteractionActivationSuppressionReason.None);
    }

    internal static bool ShouldSuppress(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchSnapshot snapshot,
        DateTime lastPreviewMouseDownUtc,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int launcherOnlySuppressionMs = ToolbarInteractionActivationSuppressionDefaults.LauncherOnlyAfterPreviewSuppressionMs)
    {
        return Resolve(
            trigger,
            snapshot,
            lastPreviewMouseDownUtc,
            lastRetouchUtc,
            nowUtc,
            launcherOnlySuppressionMs).ShouldSuppress;
    }
}
