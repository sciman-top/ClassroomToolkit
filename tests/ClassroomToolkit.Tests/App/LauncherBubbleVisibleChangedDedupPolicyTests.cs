using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldApply_WhenNoHistory()
    {
        var now = DateTime.UtcNow;
        var decision = LauncherBubbleVisibleChangedDedupPolicy.Resolve(
            currentVisibleState: true,
            lastVisibleState: null,
            lastEventUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: now);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.NoHistory);
        decision.LastVisibleState.Should().BeTrue();
        decision.LastEventUtc.Should().Be(now);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenSameStateWithinWindow()
    {
        var now = DateTime.UtcNow;
        var last = now.AddMilliseconds(-40);
        var decision = LauncherBubbleVisibleChangedDedupPolicy.Resolve(
            currentVisibleState: true,
            lastVisibleState: true,
            lastEventUtc: last,
            nowUtc: now,
            minIntervalMs: 90);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow);
        decision.LastVisibleState.Should().BeTrue();
        decision.LastEventUtc.Should().Be(last);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenStateChangesWithinWindow()
    {
        var now = DateTime.UtcNow;
        var decision = LauncherBubbleVisibleChangedDedupPolicy.Resolve(
            currentVisibleState: false,
            lastVisibleState: true,
            lastEventUtc: now.AddMilliseconds(-40),
            nowUtc: now,
            minIntervalMs: 90);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.Applied);
        decision.LastVisibleState.Should().BeFalse();
        decision.LastEventUtc.Should().Be(now);
    }

    [Fact]
    public void Resolve_RuntimeStateOverload_ShouldRespectState()
    {
        var now = DateTime.UtcNow;
        var state = new LauncherBubbleVisibleChangedRuntimeState(
            LastVisibleState: true,
            LastEventUtc: now.AddMilliseconds(-30));

        var decision = LauncherBubbleVisibleChangedDedupPolicy.Resolve(
            currentVisibleState: true,
            state,
            nowUtc: now,
            minIntervalMs: 90);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow);
        decision.LastVisibleState.Should().BeTrue();
    }
}
