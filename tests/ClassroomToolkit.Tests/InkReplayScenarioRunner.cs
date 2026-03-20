using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public enum InkReplayActionType
{
    FastRefreshImmediate = 0,
    DeferredRefreshScheduled = 1,
    DeferredRefreshRequested = 2,
    ReplayFlushed = 3,
    PointerUpTracked = 4,
    FirstInputTraceEnded = 5,
    InkContextRefreshRequested = 6
}

public sealed record InkReplayAction(
    InkReplayActionType Type,
    string? Source = null);

public sealed record InkReplayRunResult(
    IReadOnlyList<InkReplayAction> Actions,
    int PointerUpCount,
    int CrossPageSwitchCount);

public static class InkReplayScenarioRunner
{
    public static InkReplayRunResult RunCrossPagePointerUpPipeline(
        InkReplayScenario scenario,
        bool crossPageDisplayActive = true,
        bool pendingInkContextCheck = false,
        bool updatePending = false,
        bool crossPageFirstInputTraceActive = false)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var actions = new List<InkReplayAction>();
        var deferredByInkInput = false;
        var inkOperationActive = false;
        var pointerUpCount = 0;
        var crossPageSwitchCount = 0;

        foreach (var ev in scenario.Events)
        {
            switch (ev.Type)
            {
                case InkReplayEventType.PointerDown:
                case InkReplayEventType.PointerMove:
                    inkOperationActive = true;
                    break;

                case InkReplayEventType.CrossPageSwitch:
                    // In runtime this typically marks input-deferred refresh demand.
                    deferredByInkInput = true;
                    crossPageSwitchCount++;
                    break;

                case InkReplayEventType.PointerUp:
                {
                    pointerUpCount++;
                    var deferredState = CrossPagePointerUpDeferredStatePolicy.Resolve(
                        deferredByInkInput: deferredByInkInput,
                        crossPageDisplayActive: crossPageDisplayActive);
                    deferredByInkInput = deferredState.NextDeferredByInkInput;

                    var decision = CrossPagePointerUpDecisionPolicy.Resolve(
                        crossPageDisplayActive: crossPageDisplayActive,
                        hadInkOperation: inkOperationActive,
                        deferredRefreshRequested: deferredState.DeferredRefreshRequested,
                        updatePending: updatePending);

                    var executionPlan = CrossPagePointerUpExecutionPlanPolicy.Resolve(
                        decision,
                        hadInkOperation: inkOperationActive,
                        pendingInkContextCheck: pendingInkContextCheck);

                    var postPlan = CrossPagePointerUpPostExecutionPolicy.Resolve(
                        executionPlan,
                        crossPageFirstInputTraceActive: crossPageFirstInputTraceActive);

                    if (postPlan.ShouldApplyFastRefresh && decision.ShouldRequestImmediateRefresh)
                    {
                        actions.Add(new InkReplayAction(
                            InkReplayActionType.FastRefreshImmediate,
                            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PointerUpFast)));
                    }

                    if (postPlan.ShouldScheduleDeferredRefresh)
                    {
                        actions.Add(new InkReplayAction(
                            InkReplayActionType.DeferredRefreshScheduled,
                            postPlan.DeferredRefreshSource));
                    }

                    if (postPlan.ShouldFlushReplay)
                    {
                        actions.Add(new InkReplayAction(
                            InkReplayActionType.ReplayFlushed));
                    }

                    if (postPlan.ShouldTrackPointerUp)
                    {
                        actions.Add(new InkReplayAction(
                            InkReplayActionType.PointerUpTracked));
                    }

                    if (postPlan.ShouldEndFirstInputTrace)
                    {
                        actions.Add(new InkReplayAction(
                            InkReplayActionType.FirstInputTraceEnded));
                    }

                    if (postPlan.ShouldRequestInkContextRefresh)
                    {
                        actions.Add(new InkReplayAction(
                            InkReplayActionType.InkContextRefreshRequested));
                    }

                    inkOperationActive = false;
                    break;
                }
            }
        }

        return new InkReplayRunResult(
            Actions: actions,
            PointerUpCount: pointerUpCount,
            CrossPageSwitchCount: crossPageSwitchCount);
    }

    public static async Task<InkReplayRunResult> RunDeferredRefreshCoordinatorPipelineAsync(
        InkReplayScenario scenario,
        string source,
        bool singlePerPointerUp = true,
        int configuredDelayMs = 120,
        int? delayOverrideMs = null,
        int elapsedSincePointerUpMs = 10,
        bool crossPageDisplayActive = true,
        bool crossPageInteractionActive = false,
        bool dispatchSynchronously = true,
        bool forceDelayFailure = false,
        bool forceDispatchFailure = false,
        bool dispatcherCheckAccess = true,
        long initialAppliedSequence = 0)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var actions = new List<InkReplayAction>();
        var pointerUpCount = scenario.Events.Count(e => e.Type == InkReplayEventType.PointerUp);
        var crossPageSwitchCount = scenario.Events.Count(e => e.Type == InkReplayEventType.CrossPageSwitch);
        var lastPointerUpMs = scenario.Events
            .Where(e => e.Type == InkReplayEventType.PointerUp)
            .Select(e => e.TimestampMs)
            .DefaultIfEmpty(long.MinValue)
            .Max();

        var nowBase = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var hasPointerUp = lastPointerUpMs != long.MinValue;
        var lastPointerUpUtc = hasPointerUp
            ? nowBase.AddMilliseconds(lastPointerUpMs)
            : CrossPageRuntimeDefaults.UnsetTimestampUtc;
        var nowUtc = hasPointerUp
            ? lastPointerUpUtc.AddMilliseconds(Math.Max(0, elapsedSincePointerUpMs))
            : nowBase;

        var refreshToken = 0;
        long appliedSequence = initialAppliedSequence;
        long pointerUpSequence = Math.Max(1, pointerUpCount);

        var result = await CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: source,
            singlePerPointerUp: singlePerPointerUp,
            delayOverrideMs: delayOverrideMs,
            configuredDelayMs: configuredDelayMs,
            lastPointerUpUtc: lastPointerUpUtc,
            getCurrentUtcTimestamp: () => nowUtc,
            isCrossPageDisplayActive: () => crossPageDisplayActive,
            isCrossPageInteractionActive: () => crossPageInteractionActive,
            tryAcquirePostInputRefreshSlot: (out long seq) =>
            {
                seq = pointerUpSequence;
                var acquireResult = CrossPagePostInputRefreshSlotCoordinator.TryAcquire(
                    pointerUpSequence,
                    lastPointerUpUtc,
                    readAppliedSequence: () => appliedSequence,
                    compareExchangeAppliedSequence: (nextValue, comparand) =>
                    {
                        if (appliedSequence == comparand)
                        {
                            appliedSequence = nextValue;
                            return comparand;
                        }

                        return appliedSequence;
                    });
                return acquireResult.Acquired;
            },
            requestCrossPageDisplayUpdate: requestedSource =>
            {
                if (requestedSource.EndsWith(CrossPageUpdateSources.ImmediateSuffix, StringComparison.Ordinal))
                {
                    actions.Add(new InkReplayAction(InkReplayActionType.FastRefreshImmediate, requestedSource));
                }
                else if (requestedSource.EndsWith(CrossPageUpdateSources.DelayedSuffix, StringComparison.Ordinal))
                {
                    actions.Add(new InkReplayAction(InkReplayActionType.DeferredRefreshRequested, requestedSource));
                }
                else
                {
                    actions.Add(new InkReplayAction(InkReplayActionType.DeferredRefreshRequested, requestedSource));
                }
            },
            tryBeginInvoke: (action, _) =>
            {
                if (forceDispatchFailure || !dispatchSynchronously)
                {
                    return false;
                }

                action();
                return true;
            },
            delayAsync: _ =>
            {
                if (forceDelayFailure)
                {
                    return Task.FromException(new InvalidOperationException("forced-delay-failure"));
                }

                return Task.CompletedTask;
            },
            incrementRefreshToken: () => Interlocked.Increment(ref refreshToken),
            readRefreshToken: () => refreshToken,
            dispatcherCheckAccess: () => dispatcherCheckAccess,
            dispatcherShutdownStarted: () => false,
            dispatcherShutdownFinished: () => false,
            diagnostics: (_, _, _) => { });

        if (result.ScheduledDelayedRefresh)
        {
            actions.Add(new InkReplayAction(
                InkReplayActionType.DeferredRefreshScheduled,
                source));
        }

        return new InkReplayRunResult(
            Actions: actions,
            PointerUpCount: pointerUpCount,
            CrossPageSwitchCount: crossPageSwitchCount);
    }
}
