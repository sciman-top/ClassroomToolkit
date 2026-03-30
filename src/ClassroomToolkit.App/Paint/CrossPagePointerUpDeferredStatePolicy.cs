namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpDeferredStateResult(
    bool NextDeferredByInkInput,
    bool DeferredRefreshRequested,
    bool ShouldLogStableRecover);

internal static class CrossPagePointerUpDeferredStatePolicy
{
    internal static CrossPagePointerUpDeferredStateResult Resolve(
        bool deferredByInkInput,
        bool crossPageDisplayActive)
    {
        var deferredRefreshRequested = deferredByInkInput;
        var nextDeferredByInkInput = deferredByInkInput;
        var decision = CrossPagePointerUpDeferredRefreshPolicy.Resolve(
            deferredByInkInput: deferredByInkInput,
            crossPageDisplayActive: crossPageDisplayActive);
        if (decision.ShouldConsumeDeferredFlag)
        {
            nextDeferredByInkInput = false;
            if (decision.ShouldRequestPostRefresh)
            {
                deferredRefreshRequested = true;
            }
        }

        return new CrossPagePointerUpDeferredStateResult(
            NextDeferredByInkInput: nextDeferredByInkInput,
            DeferredRefreshRequested: deferredRefreshRequested,
            ShouldLogStableRecover: decision.ShouldConsumeDeferredFlag && decision.ShouldRequestPostRefresh);
    }
}
