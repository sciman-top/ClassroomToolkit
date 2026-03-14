namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDeferredRefreshPolicy
{
    internal static bool ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode refreshMode)
    {
        return refreshMode == CrossPageInteractiveSwitchRefreshMode.DeferredByInput;
    }

    internal static bool ShouldRunOnPointerUp(
        bool deferredByInkInput,
        bool crossPageDisplayActive)
    {
        return deferredByInkInput && crossPageDisplayActive;
    }
}
