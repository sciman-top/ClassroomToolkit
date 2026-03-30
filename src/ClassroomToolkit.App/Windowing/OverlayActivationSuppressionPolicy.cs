namespace ClassroomToolkit.App.Windowing;

internal enum OverlayActivationSuppressionReason
{
    None = 0,
    SuppressionRequested = 1
}

internal readonly record struct OverlayActivationSuppressionDecision(
    bool ShouldSuppress,
    OverlayActivationSuppressionReason Reason);

internal static class OverlayActivationSuppressionPolicy
{
    internal static OverlayActivationSuppressionDecision Resolve(bool suppressNextOverlayActivatedZOrderApply)
    {
        return suppressNextOverlayActivatedZOrderApply
            ? new OverlayActivationSuppressionDecision(
                ShouldSuppress: true,
                Reason: OverlayActivationSuppressionReason.SuppressionRequested)
            : new OverlayActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: OverlayActivationSuppressionReason.None);
    }

    internal static bool ShouldSuppress(bool suppressNextOverlayActivatedZOrderApply)
    {
        return Resolve(suppressNextOverlayActivatedZOrderApply).ShouldSuppress;
    }
}
