using System.Threading.Tasks;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayScenarioRunnerTests
{
    [Fact]
    public void RunCrossPagePointerUpPipeline_ShouldEmitExpectedActions_ForCrossPageSeamScenario()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-seam-basic.json");

        var result = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);

        result.PointerUpCount.Should().Be(1);
        result.CrossPageSwitchCount.Should().Be(1);

        result.Actions.Should().Contain(action =>
            action.Type == InkReplayActionType.FastRefreshImmediate
            && action.Source == CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PointerUpFast));

        result.Actions.Should().Contain(action =>
            action.Type == InkReplayActionType.DeferredRefreshScheduled
            && action.Source == CrossPagePointerUpRefreshSourcePolicy.PointerUpInk);

        result.Actions.Should().Contain(action => action.Type == InkReplayActionType.ReplayFlushed);
        result.Actions.Should().Contain(action => action.Type == InkReplayActionType.PointerUpTracked);
        result.Actions.Should().Contain(action => action.Type == InkReplayActionType.FirstInputTraceEnded);
        result.Actions.Should().Contain(action => action.Type == InkReplayActionType.InkContextRefreshRequested);
    }

    [Fact]
    public void RunCrossPagePointerUpPipeline_ShouldSuppressImmediateRefresh_WhenUpdatePending()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-seam-basic.json");

        var result = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: false,
            updatePending: true,
            crossPageFirstInputTraceActive: false);

        result.Actions.Should().NotContain(action =>
            action.Type == InkReplayActionType.FastRefreshImmediate);

        result.Actions.Should().Contain(action =>
            action.Type == InkReplayActionType.DeferredRefreshScheduled
            && action.Source == CrossPagePointerUpRefreshSourcePolicy.PointerUpInk);
    }

    [Fact]
    public async Task RunDeferredRefreshCoordinatorPipelineAsync_ShouldEmitDelayedNeighborRenderRefresh()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");

        var result = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: CrossPageUpdateSources.NeighborRender,
            singlePerPointerUp: true,
            configuredDelayMs: 60,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: 10,
            crossPageDisplayActive: true,
            crossPageInteractionActive: false,
            dispatchSynchronously: true);

        result.Actions.Should().ContainInOrder(
            new InkReplayAction(
                InkReplayActionType.DeferredRefreshRequested,
                CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.NeighborRender)),
            new InkReplayAction(
                InkReplayActionType.DeferredRefreshScheduled,
                CrossPageUpdateSources.NeighborRender));
    }

    [Fact]
    public void RunCrossPagePointerUpPipeline_ShouldTrackDpiSwitchLikeScenario()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-dpi-switch-pointerup.json");

        var result = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);

        result.PointerUpCount.Should().Be(1);
        result.CrossPageSwitchCount.Should().Be(2);
        result.Actions.Should().ContainInOrder(
            new InkReplayAction(
                InkReplayActionType.FastRefreshImmediate,
                CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PointerUpFast)),
            new InkReplayAction(
                InkReplayActionType.DeferredRefreshScheduled,
                CrossPagePointerUpRefreshSourcePolicy.PointerUpInk));
    }

    [Fact]
    public void RunCrossPagePointerUpPipeline_ShouldTrackRapidFlipScenario()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-rapid-flip-pointerup.json");

        var result = InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
            scenario,
            crossPageDisplayActive: true,
            pendingInkContextCheck: true,
            updatePending: false,
            crossPageFirstInputTraceActive: true);

        result.PointerUpCount.Should().Be(1);
        result.CrossPageSwitchCount.Should().Be(3);
        result.Actions.Should().ContainInOrder(
            new InkReplayAction(
                InkReplayActionType.FastRefreshImmediate,
                CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PointerUpFast)),
            new InkReplayAction(
                InkReplayActionType.DeferredRefreshScheduled,
                CrossPagePointerUpRefreshSourcePolicy.PointerUpInk));
    }
}
