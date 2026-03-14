using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenNoPreviousRetouch()
    {
        var now = DateTime.UtcNow;
        var decision = ToolbarInteractionRetouchThrottlePolicy.Resolve(
            WindowDedupDefaults.UnsetTimestampUtc,
            now,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchThrottleReason.FirstRetouch);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenWithinThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = ToolbarInteractionRetouchThrottlePolicy.Resolve(
            now.AddMilliseconds(-80),
            now,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchThrottleReason.WithinThrottleWindow);
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenOutsideThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = ToolbarInteractionRetouchThrottlePolicy.Resolve(
            now.AddMilliseconds(-160),
            now,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchThrottleReason.OutsideThrottleWindow);
    }

    [Fact]
    public void ShouldAllowRetouch_ShouldMapResolveDecision()
    {
        var now = DateTime.UtcNow;
        var allow = ToolbarInteractionRetouchThrottlePolicy.ShouldAllowRetouch(
            now.AddMilliseconds(-160),
            now,
            minimumIntervalMs: 120);

        allow.Should().BeTrue();
    }
}
