using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public sealed record InkReplayGoldenRecipe(
    string FileName,
    Func<Task<IReadOnlyList<InkReplayAction>>> ExecuteAsync);

public static class InkReplayGoldenRecipeCatalog
{
    public static IReadOnlyList<InkReplayGoldenRecipe> GetAll()
    {
        return
        [
            new InkReplayGoldenRecipe(
                "crosspage-seam-basic.actions.json",
                () => RunPointerUpAsync("crosspage-seam-basic.json")),
            new InkReplayGoldenRecipe(
                "crosspage-dpi-switch-pointerup.actions.json",
                () => RunPointerUpAsync("crosspage-dpi-switch-pointerup.json")),
            new InkReplayGoldenRecipe(
                "crosspage-rapid-flip-pointerup.actions.json",
                () => RunPointerUpAsync("crosspage-rapid-flip-pointerup.json")),
            new InkReplayGoldenRecipe(
                "crosspage-neighbor-render-postinput.actions.json",
                () => RunDeferredAsync(CrossPageUpdateSources.NeighborRender, 60, 10)),
            new InkReplayGoldenRecipe(
                "crosspage-postinput-immediate.actions.json",
                () => RunDeferredAsync(CrossPageUpdateSources.PostInput, 120, 500)),
            new InkReplayGoldenRecipe(
                "crosspage-neighbor-render-recover.actions.json",
                () => RunDeferredAsync(
                    CrossPageUpdateSources.NeighborRender,
                    60,
                    10,
                    forceDispatchFailure: true,
                    dispatcherCheckAccess: true)),
            new InkReplayGoldenRecipe(
                "crosspage-neighbor-render-skipped.actions.json",
                () => RunDeferredAsync(
                    CrossPageUpdateSources.NeighborRender,
                    60,
                    10,
                    crossPageInteractionActive: true)),
            new InkReplayGoldenRecipe(
                "crosspage-interaction-replay-delayed.actions.json",
                () => RunDeferredAsync(CrossPageUpdateSources.InteractionReplay, 80, 10)),
            new InkReplayGoldenRecipe(
                "crosspage-interaction-replay-delay-failure-recover.actions.json",
                () => RunDeferredAsync(
                    CrossPageUpdateSources.InteractionReplay,
                    80,
                    10,
                    forceDelayFailure: true,
                    dispatcherCheckAccess: true)),
            new InkReplayGoldenRecipe(
                "crosspage-postinput-slot-used.actions.json",
                () => RunDeferredAsync(
                    CrossPageUpdateSources.PostInput,
                    120,
                    500,
                    initialAppliedSequence: 1)),
            new InkReplayGoldenRecipe(
                "crosspage-interaction-replay-delay-failure-abort.actions.json",
                () => RunDeferredAsync(
                    CrossPageUpdateSources.InteractionReplay,
                    80,
                    10,
                    forceDelayFailure: true,
                    forceDispatchFailure: true,
                    dispatcherCheckAccess: false)),
            new InkReplayGoldenRecipe(
                "crosspage-neighbor-missing-delayed.actions.json",
                () => RunDeferredAsync(CrossPageUpdateSources.NeighborMissing, 80, 10)),
            new InkReplayGoldenRecipe(
                "crosspage-ink-visual-sync-replay-delayed.actions.json",
                () => RunDeferredAsync(CrossPageUpdateSources.InkVisualSyncReplay, 80, 10)),
            new InkReplayGoldenRecipe(
                "crosspage-neighbor-sidecar-delayed.actions.json",
                () => RunDeferredAsync(CrossPageUpdateSources.NeighborSidecar, 80, 10))
        ];
    }

    private static Task<IReadOnlyList<InkReplayAction>> RunPointerUpAsync(string scenarioFileName)
    {
        var scenario = InkReplayFixtureCatalog.Load(scenarioFileName);
        var result = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);
        return Task.FromResult(result.Actions);
    }

    private static async Task<IReadOnlyList<InkReplayAction>> RunDeferredAsync(
        string source,
        int configuredDelayMs,
        int elapsedSincePointerUpMs,
        bool crossPageDisplayActive = true,
        bool crossPageInteractionActive = false,
        bool forceDelayFailure = false,
        bool forceDispatchFailure = false,
        bool dispatcherCheckAccess = true,
        long initialAppliedSequence = 0)
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var result = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: source,
            singlePerPointerUp: true,
            configuredDelayMs: configuredDelayMs,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: elapsedSincePointerUpMs,
            crossPageDisplayActive: crossPageDisplayActive,
            crossPageInteractionActive: crossPageInteractionActive,
            dispatchSynchronously: true,
            forceDelayFailure: forceDelayFailure,
            forceDispatchFailure: forceDispatchFailure,
            dispatcherCheckAccess: dispatcherCheckAccess,
            initialAppliedSequence: initialAppliedSequence);
        return result.Actions;
    }
}
