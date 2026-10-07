
namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpDecision(
    bool ShouldTrackPointerUp,
    bool ShouldSchedulePostInputRefresh,
    bool ShouldFlushReplay,
    bool ShouldRequestImmediateRefresh);

internal readonly record struct CrossPagePointerUpDeferredRefreshDecision(
    bool ShouldConsumeDeferredFlag,
    bool ShouldRequestPostRefresh);

internal readonly record struct CrossPagePointerUpDeferredStateResult(
    bool NextDeferredByInkInput,
    bool DeferredRefreshRequested,
    bool ShouldLogStableRecover);

internal readonly record struct CrossPagePointerUpExecutionPlan(
    bool ShouldTrackPointerUp,
    bool ShouldApplyFastRefresh,
    bool ShouldScheduleDeferredRefresh,
    string DeferredRefreshSource,
    bool ShouldFlushReplay,
    bool ShouldRequestInkContextRefresh);

internal readonly record struct CrossPagePointerUpPostExecutionPlan(
    bool ShouldTrackPointerUp,
    bool ShouldApplyFastRefresh,
    bool ShouldScheduleDeferredRefresh,
    string DeferredRefreshSource,
    bool ShouldFlushReplay,
    bool ShouldEndFirstInputTrace,
    bool ShouldRequestInkContextRefresh);

internal readonly record struct CrossPagePointerUpState(
    bool CrossPageDisplayActive,
    bool PhotoTransformActive);

internal static class CrossPagePointerUpPolicies
{
    internal static CrossPagePointerUpDecision ResolveDecision(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested,
        bool updatePending)
    {
        var shouldSchedule = CrossPagePointerUpPolicies.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive,
            hadInkOperation,
            deferredRefreshRequested);
        var shouldRequestImmediateRefresh = CrossPagePointerUpPolicies.ShouldRequest(
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

    internal static CrossPagePointerUpDeferredRefreshDecision ResolveDeferredRefresh(
        bool deferredByInkInput,
        bool crossPageDisplayActive)
    {
        if (!deferredByInkInput)
        {
            return new CrossPagePointerUpDeferredRefreshDecision(
                ShouldConsumeDeferredFlag: false,
                ShouldRequestPostRefresh: false);
        }

        var shouldRequest = CrossPageRefreshCoordinationPolicies.ShouldRunOnPointerUp(
            deferredByInkInput: deferredByInkInput,
            crossPageDisplayActive: crossPageDisplayActive);
        return new CrossPagePointerUpDeferredRefreshDecision(
            ShouldConsumeDeferredFlag: true,
            ShouldRequestPostRefresh: shouldRequest);
    }

    internal static CrossPagePointerUpDeferredStateResult ResolveDeferredState(
        bool deferredByInkInput,
        bool crossPageDisplayActive)
    {
        var deferredRefreshRequested = deferredByInkInput;
        var nextDeferredByInkInput = deferredByInkInput;
        var decision = CrossPagePointerUpPolicies.ResolveDeferredRefresh(
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

    internal static CrossPagePointerUpExecutionPlan ResolveExecutionPlan(
        CrossPagePointerUpDecision decision,
        bool hadInkOperation,
        bool pendingInkContextCheck)
    {
        return new CrossPagePointerUpExecutionPlan(
            ShouldTrackPointerUp: decision.ShouldTrackPointerUp,
            ShouldApplyFastRefresh: decision.ShouldSchedulePostInputRefresh,
            ShouldScheduleDeferredRefresh: decision.ShouldSchedulePostInputRefresh,
            DeferredRefreshSource: CrossPagePointerUpPolicies.ResolveRefreshSource(hadInkOperation),
            ShouldFlushReplay: decision.ShouldFlushReplay,
            ShouldRequestInkContextRefresh: pendingInkContextCheck);
    }

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

    internal static CrossPagePointerUpPostExecutionPlan ResolvePostExecution(
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

    internal const string PointerUp = "pointer-up";
    internal const string PointerUpInk = "pointer-up-ink";

    internal static string ResolveRefreshSource(bool hadInkOperation)
    {
        return hadInkOperation ? PointerUpInk : PointerUp;
    }

    internal static CrossPagePointerUpState ResolveState(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        var photoTransformActive = PhotoWindowPolicies.IsPhotoTransformEnabled(
            photoModeActive,
            boardActive);
        var crossPageDisplayActive = crossPageDisplayEnabled && photoTransformActive;

        return new CrossPagePointerUpState(
            CrossPageDisplayActive: crossPageDisplayActive,
            PhotoTransformActive: photoTransformActive);
    }
}
