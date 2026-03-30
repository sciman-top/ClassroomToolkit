namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchThrottleReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionRetouchThrottleReason reason)
    {
        return reason switch
        {
            ToolbarInteractionRetouchThrottleReason.FirstRetouch => "first-retouch",
            ToolbarInteractionRetouchThrottleReason.IntervalDisabled => "interval-disabled",
            ToolbarInteractionRetouchThrottleReason.WithinThrottleWindow => "within-throttle-window",
            ToolbarInteractionRetouchThrottleReason.OutsideThrottleWindow => "outside-throttle-window",
            _ => "allow"
        };
    }
}
