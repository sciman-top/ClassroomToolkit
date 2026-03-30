using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageImmediateDispatchPolicyTests
{
    [Fact]
    public void Resolve_ShouldUpgradeDelayedToDirect_ForImmediateSuffix()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Delayed,
            DelayMs: 12);

        var result = CrossPageImmediateDispatchPolicy.Resolve(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Direct);
        result.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldKeepDecision_ForNonImmediateSuffix()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Delayed,
            DelayMs: 12);

        var result = CrossPageImmediateDispatchPolicy.Resolve(
            decision,
            CrossPageUpdateDispatchSuffix.None);

        result.Should().Be(decision);
    }

    [Fact]
    public void Resolve_ShouldKeepNonDelayedMode_ForImmediateSuffix()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);

        var result = CrossPageImmediateDispatchPolicy.Resolve(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate);

        result.Should().Be(decision);
    }
}
