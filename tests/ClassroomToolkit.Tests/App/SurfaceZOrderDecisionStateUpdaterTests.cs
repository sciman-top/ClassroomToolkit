using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SurfaceZOrderDecisionStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldUpdateTrackedDecisionAndTimestamp()
    {
        var state = SurfaceZOrderDecisionRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 11, 0, 0, DateTimeKind.Utc);
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
        var dedup = new SurfaceZOrderDecisionDedupDecision(
            ShouldApply: true,
            LastDecision: decision,
            LastAppliedUtc: nowUtc,
            Reason: SurfaceZOrderDecisionDedupReason.Applied);

        SurfaceZOrderDecisionStateUpdater.Apply(ref state, dedup);

        state.LastDecision.Should().Be(decision);
        state.LastAppliedUtc.Should().Be(nowUtc);
    }
}
