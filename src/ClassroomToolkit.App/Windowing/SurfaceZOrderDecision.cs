using
ClassroomToolkit.App.Session;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal static class UiSceneSurfaceMapper
{
    internal static ZOrderSurface Map(UiSceneKind scene)
    {
        return scene switch
        {
            UiSceneKind.PresentationFullscreen => ZOrderSurface.PresentationFullscreen,
            UiSceneKind.PhotoFullscreen => ZOrderSurface.PhotoFullscreen,
            UiSceneKind.Whiteboard => ZOrderSurface.Whiteboard,
            _ => ZOrderSurface.None
        };
    }
}

internal readonly record struct SurfaceZOrderDecision(
    bool ShouldTouchSurface,
    ZOrderSurface Surface,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal readonly record struct SurfaceZOrderDecisionRuntimeState(
    SurfaceZOrderDecision? LastDecision,
    DateTime LastAppliedUtc)
{
    internal static SurfaceZOrderDecisionRuntimeState Default => new(
        LastDecision: null,
        LastAppliedUtc: WindowDedupDefaults.UnsetTimestampUtc);
}

internal static class SurfaceZOrderDecisionStateUpdater
{
    internal static void Apply(
        ref SurfaceZOrderDecisionRuntimeState state,
        SurfaceZOrderDecisionDedupDecision dedupDecision)
    {
        state = new SurfaceZOrderDecisionRuntimeState(
            LastDecision: dedupDecision.LastDecision,
            LastAppliedUtc: dedupDecision.LastAppliedUtc);
    }

    internal static void Apply(
        ref SurfaceZOrderDecision? lastDecision,
        ref DateTime lastAppliedUtc,
        SurfaceZOrderDecisionDedupDecision dedupDecision)
    {
        lastDecision = dedupDecision.LastDecision;
        lastAppliedUtc = dedupDecision.LastAppliedUtc;
    }
}

internal static class SurfaceZOrderDecisionDedupIntervalPolicy
{
    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs,
        int interactiveMs = FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
    {
        return InteractiveSceneIntervalPolicy.ResolveMs(
            overlayVisible,
            photoModeActive,
            whiteboardActive,
            defaultMs,
            interactiveMs);
    }
}

internal readonly record struct SurfaceZOrderDecisionDedupDecision(
    bool ShouldApply,
    SurfaceZOrderDecision LastDecision,
    DateTime LastAppliedUtc,
    SurfaceZOrderDecisionDedupReason Reason);

internal enum SurfaceZOrderDecisionDedupReason
{
    None = 0,
    NoHistory = 1,
    DedupDisabledByInterval = 2,
    UnsetTimestamp = 3,
    SkippedWithinDedupWindow = 4,
    Applied = 5
}

internal static class SurfaceZOrderDecisionDedupPolicy
{
    internal static SurfaceZOrderDecisionDedupDecision Resolve(
        SurfaceZOrderDecision currentDecision,
        SurfaceZOrderDecisionRuntimeState state,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        return Resolve(
            currentDecision,
            state.LastDecision,
            state.LastAppliedUtc,
            nowUtc,
            minIntervalMs);
    }

    internal static SurfaceZOrderDecisionDedupDecision Resolve(
        SurfaceZOrderDecision currentDecision,
        SurfaceZOrderDecision? lastDecision,
        DateTime lastAppliedUtc,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        if (!lastDecision.HasValue
            || minIntervalMs <= WindowDedupDefaults.MinIntervalMs
            || lastAppliedUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            var reason = !lastDecision.HasValue
                ? SurfaceZOrderDecisionDedupReason.NoHistory
                : minIntervalMs <= WindowDedupDefaults.MinIntervalMs
                    ? SurfaceZOrderDecisionDedupReason.DedupDisabledByInterval
                    : SurfaceZOrderDecisionDedupReason.UnsetTimestamp;
            return new SurfaceZOrderDecisionDedupDecision(
                ShouldApply: true,
                LastDecision: currentDecision,
                LastAppliedUtc: nowUtc,
                Reason: reason);
        }

        if (!currentDecision.ForceEnforceZOrder
            && currentDecision.Equals(lastDecision.Value)
            && (nowUtc - lastAppliedUtc).TotalMilliseconds < minIntervalMs)
        {
            return new SurfaceZOrderDecisionDedupDecision(
                ShouldApply: false,
                LastDecision: lastDecision.Value,
                LastAppliedUtc: lastAppliedUtc,
                Reason: SurfaceZOrderDecisionDedupReason.SkippedWithinDedupWindow);
        }

        return new SurfaceZOrderDecisionDedupDecision(
            ShouldApply: true,
            LastDecision: currentDecision,
            LastAppliedUtc: nowUtc,
            Reason: SurfaceZOrderDecisionDedupReason.Applied);
    }
}
