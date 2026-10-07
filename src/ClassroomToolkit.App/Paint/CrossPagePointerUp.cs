
namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpDecision(
    bool ShouldTrackPointerUp,
    bool ShouldSchedulePostInputRefresh,
    bool ShouldFlushReplay,
    bool ShouldRequestImmediateRefresh);

internal static class CrossPagePointerUpDecisionPolicy
{
    internal static CrossPagePointerUpDecision Resolve(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested,
        bool updatePending)
    {
        var shouldSchedule = CrossPagePointerUpRefreshPolicy.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive,
            hadInkOperation,
            deferredRefreshRequested);
        var shouldRequestImmediateRefresh = CrossPagePointerUpImmediateRefreshPolicy.ShouldRequest(
            crossPageDisplayActive,
            hadInkOperation,
            deferredRefreshRequested,
            updatePending);

        return new CrossPagePointerUpDecision(
            ShouldTrackPointerUp: crossPageDisplayActive,
            ShouldSchedulePostInputRefresh: shouldSchedule,
            ShouldFlushReplay: crossPageDisplayActive,
            ShouldRequestImmediateRefresh: shouldRequestImmediateRefresh);
    }
}

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

internal readonly record struct CrossPagePointerUpExecutionPlan(
    bool ShouldTrackPointerUp,
    bool ShouldApplyFastRefresh,
    bool ShouldScheduleDeferredRefresh,
    string DeferredRefreshSource,
    bool ShouldFlushReplay,
    bool ShouldRequestInkContextRefresh);

internal static class CrossPagePointerUpExecutionPlanPolicy
{
    internal static CrossPagePointerUpExecutionPlan Resolve(
        CrossPagePointerUpDecision decision,
        bool hadInkOperation,
        bool pendingInkContextCheck)
    {
        return new CrossPagePointerUpExecutionPlan(
            ShouldTrackPointerUp: decision.ShouldTrackPointerUp,
            ShouldApplyFastRefresh: decision.ShouldSchedulePostInputRefresh,
            ShouldScheduleDeferredRefresh: decision.ShouldSchedulePostInputRefresh,
            DeferredRefreshSource: CrossPagePointerUpRefreshSourcePolicy.Resolve(hadInkOperation),
            ShouldFlushReplay: decision.ShouldFlushReplay,
            ShouldRequestInkContextRefresh: pendingInkContextCheck);
    }
}

internal static class CrossPagePointerUpImmediateRefreshPolicy
{
    internal static bool ShouldRequest(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested,
        bool updatePending)
    {
        if (!crossPageDisplayActive)
        {
            return false;
        }

        if (updatePending)
        {
            return false;
        }

        return hadInkOperation || deferredRefreshRequested;
    }
}

internal readonly record struct CrossPagePointerUpPostExecutionPlan(
    bool ShouldTrackPointerUp,
    bool ShouldApplyFastRefresh,
    bool ShouldScheduleDeferredRefresh,
    string DeferredRefreshSource,
    bool ShouldFlushReplay,
    bool ShouldEndFirstInputTrace,
    bool ShouldRequestInkContextRefresh);

internal static class CrossPagePointerUpPostExecutionPolicy
{
    internal static CrossPagePointerUpPostExecutionPlan Resolve(
        CrossPagePointerUpExecutionPlan executionPlan,
        bool crossPageFirstInputTraceActive)
    {
        return new CrossPagePointerUpPostExecutionPlan(
            ShouldTrackPointerUp: executionPlan.ShouldTrackPointerUp,
            ShouldApplyFastRefresh: executionPlan.ShouldApplyFastRefresh,
            ShouldScheduleDeferredRefresh: executionPlan.ShouldScheduleDeferredRefresh,
            DeferredRefreshSource: executionPlan.DeferredRefreshSource,
            ShouldFlushReplay: executionPlan.ShouldFlushReplay,
            ShouldEndFirstInputTrace: crossPageFirstInputTraceActive,
            ShouldRequestInkContextRefresh: executionPlan.ShouldRequestInkContextRefresh);
    }
}

internal static class CrossPagePointerUpRefreshPolicy
{
    internal static bool ShouldSchedulePostInputRefresh(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested)
    {
        if (!crossPageDisplayActive)
        {
            return false;
        }

        return hadInkOperation || deferredRefreshRequested;
    }
}

internal static class CrossPagePointerUpRefreshSourcePolicy
{
    internal const string PointerUp = "pointer-up";
    internal const string PointerUpInk = "pointer-up-ink";

    internal static string Resolve(bool hadInkOperation)
    {
        return hadInkOperation ? PointerUpInk : PointerUp;
    }
}

internal readonly record struct CrossPagePointerUpState(
    bool CrossPageDisplayActive,
    bool PhotoTransformActive);

internal static class CrossPagePointerUpStatePolicy
{
    internal static CrossPagePointerUpState Resolve(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        var photoTransformActive = PhotoInteractionModePolicy.IsPhotoTransformEnabled(
            photoModeActive,
            boardActive);
        var crossPageDisplayActive = crossPageDisplayEnabled && photoTransformActive;

        return new CrossPagePointerUpState(
            CrossPageDisplayActive: crossPageDisplayActive,
            PhotoTransformActive: photoTransformActive);
    }
}
