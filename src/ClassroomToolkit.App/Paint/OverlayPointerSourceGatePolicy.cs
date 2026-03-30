namespace ClassroomToolkit.App.Paint;

internal enum OverlayPointerSourceGateDecision
{
    Continue = 0,
    Ignore = 1,
    Consume = 2
}

internal static class OverlayPointerSourceGatePolicy
{
    internal static OverlayPointerSourceGateDecision Resolve(
        bool photoLoading,
        bool ignoreFromPhotoControls)
    {
        if (photoLoading)
        {
            return OverlayPointerSourceGateDecision.Consume;
        }

        if (ignoreFromPhotoControls)
        {
            return OverlayPointerSourceGateDecision.Ignore;
        }

        return OverlayPointerSourceGateDecision.Continue;
    }
}
