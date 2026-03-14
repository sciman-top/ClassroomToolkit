namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionActivationSuppressionReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionActivationSuppressionReason reason)
    {
        return reason switch
        {
            ToolbarInteractionActivationSuppressionReason.NonActivatedTrigger => "non-activated-trigger",
            ToolbarInteractionActivationSuppressionReason.PreviewTimestampUnset => "preview-timestamp-unset",
            ToolbarInteractionActivationSuppressionReason.OutsideSuppressionWindow => "outside-window",
            ToolbarInteractionActivationSuppressionReason.LauncherOnlyDrift => "launcher-only-drift",
            ToolbarInteractionActivationSuppressionReason.PreviewAlreadyRetouched => "preview-already-retouched",
            _ => "not-suppressed"
        };
    }
}
