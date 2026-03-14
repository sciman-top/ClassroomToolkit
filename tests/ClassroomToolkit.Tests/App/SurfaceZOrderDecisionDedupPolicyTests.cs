using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SurfaceZOrderDecisionDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldApply_WhenNoHistory()
    {
        var now = DateTime.UtcNow;
        var current = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        var result = SurfaceZOrderDecisionDedupPolicy.Resolve(
            current,
            lastDecision: null,
            lastAppliedUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: now);

        result.ShouldApply.Should().BeTrue();
        result.LastDecision.Should().Be(current);
        result.LastAppliedUtc.Should().Be(now);
        result.Reason.Should().Be(SurfaceZOrderDecisionDedupReason.NoHistory);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenSameDecisionWithinWindow_AndNotForced()
    {
        var now = DateTime.UtcNow;
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.Whiteboard,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        var result = SurfaceZOrderDecisionDedupPolicy.Resolve(
            decision,
            lastDecision: decision,
            lastAppliedUtc: now.AddMilliseconds(-20),
            nowUtc: now,
            minIntervalMs: 90);

        result.ShouldApply.Should().BeFalse();
        result.Reason.Should().Be(SurfaceZOrderDecisionDedupReason.SkippedWithinDedupWindow);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenForcedEvenIfSameDecision()
    {
        var now = DateTime.UtcNow;
        var forced = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.Whiteboard,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true);

        var result = SurfaceZOrderDecisionDedupPolicy.Resolve(
            forced,
            lastDecision: forced,
            lastAppliedUtc: now.AddMilliseconds(-20),
            nowUtc: now,
            minIntervalMs: 90);

        result.ShouldApply.Should().BeTrue();
        result.Reason.Should().Be(SurfaceZOrderDecisionDedupReason.Applied);
    }

    [Fact]
    public void Resolve_RuntimeStateOverload_ShouldUseStateSnapshot()
    {
        var now = DateTime.UtcNow;
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.Whiteboard,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
        var state = new SurfaceZOrderDecisionRuntimeState(
            LastDecision: decision,
            LastAppliedUtc: now.AddMilliseconds(-20));

        var result = SurfaceZOrderDecisionDedupPolicy.Resolve(
            decision,
            state,
            nowUtc: now,
            minIntervalMs: 90);

        result.ShouldApply.Should().BeFalse();
        result.Reason.Should().Be(SurfaceZOrderDecisionDedupReason.SkippedWithinDedupWindow);
    }
}
