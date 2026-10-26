using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Paint;

// 仅保留 golden 基线未覆盖的参数变体；其余场景的端到端动作序列由
// InkReplayGoldenActionTests 的逐场景严格等价比对承载。
public sealed class InkReplayScenarioRunnerTests
{
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
            && action.Source == CrossPagePointerUpPolicies.PointerUpInk);
    }
}
