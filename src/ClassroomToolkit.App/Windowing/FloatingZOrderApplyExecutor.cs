namespace ClassroomToolkit.App.Windowing;

internal static class FloatingZOrderApplyExecutor
{
    internal static bool Apply(
        FloatingZOrderRequest request,
        Action<bool> requestApply)
    {
        return Apply(
            requestZOrderApply: true,
            request.ForceEnforceZOrder,
            requestApply);
    }

    internal static bool ApplyTouchResult(
        bool applyPolicy,
        bool touchChanged,
        Action<bool> requestApply)
    {
        return ApplyTouchResult(
            applyPolicy,
            touchChanged,
            forceEnforceZOrder: false,
            requestApply);
    }

    internal static bool ApplyTouchResult(
        bool applyPolicy,
        bool touchChanged,
        bool forceEnforceZOrder,
        Action<bool> requestApply)
    {
        return Apply(
            requestZOrderApply: applyPolicy && touchChanged,
            forceEnforceZOrder: forceEnforceZOrder,
            requestApply);
    }

    internal static bool Apply(
        bool requestZOrderApply,
        bool forceEnforceZOrder,
        Action<bool> requestApply)
    {
        if (!requestZOrderApply)
        {
            return false;
        }

        requestApply(forceEnforceZOrder);
        return true;
    }
}
