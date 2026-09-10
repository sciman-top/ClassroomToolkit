namespace ClassroomToolkit.App.Paint;

internal sealed record WpsHookInterceptDecision(
    bool InterceptKeyboard,
    bool InterceptWheel,
    bool BlockOnly,
    bool EmitWheelOnBlock);

internal static class WpsHookInterceptPolicy
{
    internal static WpsHookInterceptDecision Resolve(
        bool shouldEnable,
        PaintToolMode mode,
        bool targetIsSlideshow,
        bool targetForeground,
        bool isRawSendMode,
        bool wheelForward)
    {
        var blockOnly = false;
        var interceptKeyboard = true;
        // When WPS already owns the foreground, keep its native wheel path and
        // do not also inject a key from the hook.  Wheel mapping is only a
        // background-target bridge; this removes the native-wheel + injected-key
        // double channel that debounce cannot prove away.
        var interceptWheel = wheelForward && !targetForeground;
        var emitWheelOnBlock = interceptWheel;

        if (!shouldEnable)
        {
            return new WpsHookInterceptDecision(
                InterceptKeyboard: false,
                InterceptWheel: false,
                BlockOnly: false,
                EmitWheelOnBlock: false);
        }

        if (mode == PaintToolMode.Cursor)
        {
            if (targetIsSlideshow && !targetForeground)
            {
                // Cursor mode prefers passthrough, but keep keyboard fallback
                // when WPS slideshow is not foreground to avoid navigation dead zones.
                return new WpsHookInterceptDecision(
                    InterceptKeyboard: true,
                    InterceptWheel: false,
                    BlockOnly: false,
                    EmitWheelOnBlock: false);
            }

            return new WpsHookInterceptDecision(
                InterceptKeyboard: false,
                InterceptWheel: false,
                BlockOnly: false,
                EmitWheelOnBlock: false);
        }

        // In inking mode, overlay is usually foreground. Keep hook interception
        // active for presentation scene even when target isn't foreground.
        if (!targetForeground && !targetIsSlideshow)
        {
            return new WpsHookInterceptDecision(
                InterceptKeyboard: false,
                InterceptWheel: false,
                BlockOnly: false,
                EmitWheelOnBlock: false);
        }

        if (mode != PaintToolMode.Cursor && isRawSendMode)
        {
            // In drawing mode, avoid swallowing keyboard/wheel input.
            // Keep hook for remote clickers while local input still goes through.
            blockOnly = false;
            emitWheelOnBlock = wheelForward;
        }

        return new WpsHookInterceptDecision(
            InterceptKeyboard: interceptKeyboard,
            InterceptWheel: interceptWheel,
            BlockOnly: blockOnly,
            EmitWheelOnBlock: emitWheelOnBlock);
    }
}
