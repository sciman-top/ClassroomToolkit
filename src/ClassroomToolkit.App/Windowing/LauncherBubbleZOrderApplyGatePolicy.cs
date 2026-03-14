using System;

namespace ClassroomToolkit.App.Windowing;

internal enum LauncherBubbleZOrderApplyGateReason
{
    None = 0,
    AppClosing = 1,
    BubbleWindowMissing = 2,
    BubbleHidden = 3,
    VisibleChangedSuppressed = 4,
    CooldownActive = 5
}

internal readonly record struct LauncherBubbleZOrderApplyGateDecision(
    bool ShouldApply,
    LauncherBubbleZOrderApplyGateReason Reason,
    LauncherBubbleVisibleChangedApplyReason VisibleChangedReason);

internal static class LauncherBubbleZOrderApplyGatePolicy
{
    internal static LauncherBubbleZOrderApplyGateDecision Resolve(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc,
        bool appClosing,
        bool bubbleWindowExists)
    {
        if (appClosing)
        {
            return new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: false,
                Reason: LauncherBubbleZOrderApplyGateReason.AppClosing,
                VisibleChangedReason: LauncherBubbleVisibleChangedApplyReason.None);
        }

        if (!bubbleWindowExists)
        {
            return new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: false,
                Reason: LauncherBubbleZOrderApplyGateReason.BubbleWindowMissing,
                VisibleChangedReason: LauncherBubbleVisibleChangedApplyReason.None);
        }

        var visibleChangedDecision = LauncherBubbleVisibleChangedApplyPolicy.Resolve(
            bubbleVisible,
            suppressVisibleChangedApply,
            suppressVisibleChangedUntilUtc,
            nowUtc);
        return visibleChangedDecision.ShouldApply
            ? new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: true,
                Reason: LauncherBubbleZOrderApplyGateReason.None,
                VisibleChangedReason: LauncherBubbleVisibleChangedApplyReason.None)
            : new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: false,
                Reason: visibleChangedDecision.Reason switch
                {
                    LauncherBubbleVisibleChangedApplyReason.BubbleHidden => LauncherBubbleZOrderApplyGateReason.BubbleHidden,
                    LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed => LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed,
                    LauncherBubbleVisibleChangedApplyReason.CooldownActive => LauncherBubbleZOrderApplyGateReason.CooldownActive,
                    _ => LauncherBubbleZOrderApplyGateReason.CooldownActive
                },
                VisibleChangedReason: visibleChangedDecision.Reason);
    }

    internal static bool ShouldApply(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc,
        bool appClosing,
        bool bubbleWindowExists)
    {
        return Resolve(
            bubbleVisible,
            suppressVisibleChangedApply,
            suppressVisibleChangedUntilUtc,
            nowUtc,
            appClosing,
            bubbleWindowExists).ShouldApply;
    }
}
