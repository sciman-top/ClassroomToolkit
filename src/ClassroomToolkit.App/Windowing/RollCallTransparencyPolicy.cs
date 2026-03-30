namespace ClassroomToolkit.App.Windowing;

internal enum RollCallTransparencyReason
{
    None = 0,
    Enabled = 1,
    Hovering = 2,
    PaintModeDisallow = 3
}

internal readonly record struct RollCallTransparencyDecision(
    bool TransparentEnabled,
    RollCallTransparencyReason Reason);

internal enum RollCallTransparencyStyleApplyReason
{
    None = 0,
    StateChanged = 1,
    StateUnknown = 2,
    StateUnchanged = 3
}

internal readonly record struct RollCallTransparencyStyleApplyDecision(
    bool ShouldApplyStyle,
    RollCallTransparencyStyleApplyReason Reason);

internal enum RollCallHoverTimerReason
{
    None = 0,
    StartWhenTransparent = 1,
    StopWhenOpaque = 2,
    NoChange = 3
}

internal readonly record struct RollCallHoverTimerDecision(
    bool ShouldStart,
    bool ShouldStop,
    RollCallHoverTimerReason Reason);

internal static class RollCallTransparencyPolicy
{
    internal static RollCallTransparencyDecision ResolveTransparency(
        bool hovering,
        bool paintAllowsTransparency)
    {
        if (hovering)
        {
            return new RollCallTransparencyDecision(
                TransparentEnabled: false,
                Reason: RollCallTransparencyReason.Hovering);
        }

        if (!paintAllowsTransparency)
        {
            return new RollCallTransparencyDecision(
                TransparentEnabled: false,
                Reason: RollCallTransparencyReason.PaintModeDisallow);
        }

        return new RollCallTransparencyDecision(
            TransparentEnabled: true,
            Reason: RollCallTransparencyReason.Enabled);
    }

    internal static bool ShouldEnableTransparent(bool hovering, bool paintAllowsTransparency)
    {
        return ResolveTransparency(
            hovering,
            paintAllowsTransparency).TransparentEnabled;
    }

    internal static RollCallTransparencyStyleApplyDecision ResolveStyleApply(
        bool transparentEnabled,
        bool? lastTransparentEnabled)
    {
        if (!lastTransparentEnabled.HasValue)
        {
            return new RollCallTransparencyStyleApplyDecision(
                ShouldApplyStyle: true,
                Reason: RollCallTransparencyStyleApplyReason.StateUnknown);
        }

        var changed = lastTransparentEnabled.Value != transparentEnabled;
        return changed
            ? new RollCallTransparencyStyleApplyDecision(
                ShouldApplyStyle: true,
                Reason: RollCallTransparencyStyleApplyReason.StateChanged)
            : new RollCallTransparencyStyleApplyDecision(
                ShouldApplyStyle: false,
                Reason: RollCallTransparencyStyleApplyReason.StateUnchanged);
    }

    internal static bool ShouldApplyStyle(bool transparentEnabled, bool? lastTransparentEnabled)
    {
        return ResolveStyleApply(
            transparentEnabled,
            lastTransparentEnabled).ShouldApplyStyle;
    }

    internal static (int SetMask, int ClearMask) ResolveStyleMasks(bool transparentEnabled)
    {
        if (transparentEnabled)
        {
            return (WindowStyleBitMasks.WsExTransparent, 0);
        }

        return (0, WindowStyleBitMasks.WsExTransparent);
    }

    internal static bool ShouldStartHoverTimer(bool transparentEnabled, bool hoverTimerEnabled)
    {
        return ResolveHoverTimer(
            transparentEnabled,
            hoverTimerEnabled).ShouldStart;
    }

    internal static bool ShouldStopHoverTimer(bool transparentEnabled, bool hoverTimerEnabled)
    {
        return ResolveHoverTimer(
            transparentEnabled,
            hoverTimerEnabled).ShouldStop;
    }

    internal static RollCallHoverTimerDecision ResolveHoverTimer(
        bool transparentEnabled,
        bool hoverTimerEnabled)
    {
        if (transparentEnabled && !hoverTimerEnabled)
        {
            return new RollCallHoverTimerDecision(
                ShouldStart: true,
                ShouldStop: false,
                Reason: RollCallHoverTimerReason.StartWhenTransparent);
        }

        if (!transparentEnabled && hoverTimerEnabled)
        {
            return new RollCallHoverTimerDecision(
                ShouldStart: false,
                ShouldStop: true,
                Reason: RollCallHoverTimerReason.StopWhenOpaque);
        }

        return new RollCallHoverTimerDecision(
            ShouldStart: false,
            ShouldStop: false,
            Reason: RollCallHoverTimerReason.NoChange);
    }
}
