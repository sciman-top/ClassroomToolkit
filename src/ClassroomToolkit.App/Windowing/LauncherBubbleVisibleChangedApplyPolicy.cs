using System;

namespace ClassroomToolkit.App.Windowing;

internal enum LauncherBubbleVisibleChangedApplyReason
{
    None = 0,
    BubbleHidden = 1,
    VisibleChangedSuppressed = 2,
    CooldownActive = 3
}

internal readonly record struct LauncherBubbleVisibleChangedApplyDecision(
    bool ShouldApply,
    LauncherBubbleVisibleChangedApplyReason Reason);

internal static class LauncherBubbleVisibleChangedApplyPolicy
{
    internal static LauncherBubbleVisibleChangedApplyDecision Resolve(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc)
    {
        if (!bubbleVisible)
        {
            return new LauncherBubbleVisibleChangedApplyDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedApplyReason.BubbleHidden);
        }

        if (suppressVisibleChangedApply)
        {
            return new LauncherBubbleVisibleChangedApplyDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed);
        }

        if (suppressVisibleChangedUntilUtc != WindowDedupDefaults.UnsetTimestampUtc
            && nowUtc < suppressVisibleChangedUntilUtc)
        {
            return new LauncherBubbleVisibleChangedApplyDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedApplyReason.CooldownActive);
        }

        return new LauncherBubbleVisibleChangedApplyDecision(
            ShouldApply: true,
            Reason: LauncherBubbleVisibleChangedApplyReason.None);
    }

    internal static bool ShouldApplyZOrder(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc)
    {
        return Resolve(
            bubbleVisible,
            suppressVisibleChangedApply,
            suppressVisibleChangedUntilUtc,
            nowUtc).ShouldApply;
    }
}
