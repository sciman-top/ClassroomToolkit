namespace ClassroomToolkit.App.Paint;

internal static class OverlayFocusResolverGatePolicy
{
    internal static bool ShouldResolvePresentationTarget(
        bool presentationAllowed,
        bool navigationAllowsPresentationInput)
    {
        return presentationAllowed && navigationAllowsPresentationInput;
    }
}
