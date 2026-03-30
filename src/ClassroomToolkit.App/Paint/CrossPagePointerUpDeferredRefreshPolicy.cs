namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpDeferredRefreshDecision(
    bool ShouldConsumeDeferredFlag,
    bool ShouldRequestPostRefresh);

internal static class CrossPagePointerUpDeferredRefreshPolicy
{
    internal static CrossPagePointerUpDeferredRefreshDecision Resolve(
        bool deferredByInkInput,
        bool crossPageDisplayActive)
    {
        if (!deferredByInkInput)
        {
            return new CrossPagePointerUpDeferredRefreshDecision(
                ShouldConsumeDeferredFlag: false,
                ShouldRequestPostRefresh: false);
        }

        var shouldRequest = CrossPageDeferredRefreshPolicy.ShouldRunOnPointerUp(
            deferredByInkInput: deferredByInkInput,
            crossPageDisplayActive: crossPageDisplayActive);
        return new CrossPagePointerUpDeferredRefreshDecision(
            ShouldConsumeDeferredFlag: true,
            ShouldRequestPostRefresh: shouldRequest);
    }
}
