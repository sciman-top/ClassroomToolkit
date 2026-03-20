using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayGoldenActionTests
{
    [Fact]
    public void PointerUpPipelineActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-seam-basic.json");
        var actual = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-seam-basic.actions.json");

        golden.Name.Should().Be("crosspage-seam-basic");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NeighborRenderDeferredActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.NeighborRender,
            singlePerPointerUp: true,
            configuredDelayMs: 60,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-neighbor-render-postinput.actions.json");

        golden.Name.Should().Be("crosspage-neighbor-render-postinput");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task PostInputImmediateActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: true,
            configuredDelayMs: 120,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 500,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-postinput-immediate.actions.json");

        golden.Name.Should().Be("crosspage-postinput-immediate");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NeighborRenderRecoveryActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.NeighborRender,
            singlePerPointerUp: true,
            configuredDelayMs: 60,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true,
            forceDispatchFailure: true,
            dispatcherCheckAccess: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-neighbor-render-recover.actions.json");

        golden.Name.Should().Be("crosspage-neighbor-render-recover");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NeighborRenderSkippedActions_ShouldMatchGoldenBaseline_WhenInteractionActive()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.NeighborRender,
            singlePerPointerUp: true,
            configuredDelayMs: 60,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: true,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-neighbor-render-skipped.actions.json");

        golden.Name.Should().Be("crosspage-neighbor-render-skipped");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InteractionReplayDelayedActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.InteractionReplay,
            singlePerPointerUp: true,
            configuredDelayMs: 80,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-interaction-replay-delayed.actions.json");

        golden.Name.Should().Be("crosspage-interaction-replay-delayed");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InteractionReplayDelayFailureRecoveryActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.InteractionReplay,
            singlePerPointerUp: true,
            configuredDelayMs: 80,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true,
            forceDelayFailure: true,
            forceDispatchFailure: false,
            dispatcherCheckAccess: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-interaction-replay-delay-failure-recover.actions.json");

        golden.Name.Should().Be("crosspage-interaction-replay-delay-failure-recover");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task PostInputImmediateActions_ShouldBeSkipped_WhenSingleSlotAlreadyUsed()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: true,
            configuredDelayMs: 120,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 500,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true,
            initialAppliedSequence: 1);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-postinput-slot-used.actions.json");

        golden.Name.Should().Be("crosspage-postinput-slot-used");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InteractionReplayDelayFailureAbortActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.InteractionReplay,
            singlePerPointerUp: true,
            configuredDelayMs: 80,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true,
            forceDelayFailure: true,
            forceDispatchFailure: true,
            dispatcherCheckAccess: false);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-interaction-replay-delay-failure-abort.actions.json");

        golden.Name.Should().Be("crosspage-interaction-replay-delay-failure-abort");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NeighborMissingDelayedActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.NeighborMissing,
            singlePerPointerUp: true,
            configuredDelayMs: 80,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-neighbor-missing-delayed.actions.json");

        golden.Name.Should().Be("crosspage-neighbor-missing-delayed");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InkVisualSyncReplayDelayedActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.InkVisualSyncReplay,
            singlePerPointerUp: true,
            configuredDelayMs: 80,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-ink-visual-sync-replay-delayed.actions.json");

        golden.Name.Should().Be("crosspage-ink-visual-sync-replay-delayed");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NeighborSidecarDelayedActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var actual = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.NeighborSidecar,
            singlePerPointerUp: true,
            configuredDelayMs: 80,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-neighbor-sidecar-delayed.actions.json");

        golden.Name.Should().Be("crosspage-neighbor-sidecar-delayed");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void DpiSwitchPointerUpActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-dpi-switch-pointerup.json");
        var actual = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-dpi-switch-pointerup.actions.json");

        golden.Name.Should().Be("crosspage-dpi-switch-pointerup");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void RapidFlipPointerUpActions_ShouldMatchGoldenBaseline()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-rapid-flip-pointerup.json");
        var actual = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);
        var golden = InkReplayGoldenActionCatalog.Load("crosspage-rapid-flip-pointerup.actions.json");

        golden.Name.Should().Be("crosspage-rapid-flip-pointerup");
        actual.Actions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }
}
