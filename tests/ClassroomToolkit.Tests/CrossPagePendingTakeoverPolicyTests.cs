using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePendingTakeoverPolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepSkipPending_WhenNotImmediate()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 1,
            PendingSinceUtc: DateTime.UtcNow.AddMilliseconds(-500));

        var result = CrossPagePendingTakeoverPolicy.Resolve(
            decision,
            CrossPageUpdateDispatchSuffix.None,
            state,
            DateTime.UtcNow);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.SkipPending);
    }

    [Fact]
    public void Resolve_ShouldUpgradeToDirect_WhenImmediateAndPendingTimedOut()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 1,
            PendingSinceUtc: nowUtc.AddMilliseconds(-200));

        var result = CrossPagePendingTakeoverPolicy.Resolve(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate,
            state,
            nowUtc,
            thresholdMs: 120);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Direct);
        result.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldKeepSkipPending_WhenImmediateButPendingWithinThreshold()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 1,
            PendingSinceUtc: nowUtc.AddMilliseconds(-40));

        var result = CrossPagePendingTakeoverPolicy.Resolve(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate,
            state,
            nowUtc,
            thresholdMs: 120);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.SkipPending);
    }
}
