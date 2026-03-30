using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestBurstDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldQueue_WhenNoPreviousRequest()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestBurstDedupPolicy.Resolve(
            lastRequestUtc: WindowDedupDefaults.UnsetTimestampUtc,
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: ZOrderRequestBurstDedupDefaults.MinIntervalMs);

        decision.ShouldQueue.Should().BeTrue();
        decision.LastRequestUtc.Should().Be(now);
        decision.LastForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.QueuedNoHistory);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenWithinWindowAndForceFlagUnchanged()
    {
        var now = DateTime.UtcNow;
        var last = now.AddMilliseconds(-5);
        var decision = ZOrderRequestBurstDedupPolicy.Resolve(
            lastRequestUtc: last,
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: ZOrderRequestBurstDedupDefaults.MinIntervalMs);

        decision.ShouldQueue.Should().BeFalse();
        decision.LastRequestUtc.Should().Be(last);
        decision.LastForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.DedupSameForceWithinWindow);
    }

    [Fact]
    public void Resolve_ShouldQueue_WhenWithinWindowButForceFlagChanged()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestBurstDedupPolicy.Resolve(
            lastRequestUtc: now.AddMilliseconds(-5),
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: true,
            minIntervalMs: ZOrderRequestBurstDedupDefaults.MinIntervalMs);

        decision.ShouldQueue.Should().BeTrue();
        decision.LastRequestUtc.Should().Be(now);
        decision.LastForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.QueuedForceEscalationWithinWindow);
        }

    [Fact]
    public void Resolve_ShouldSkip_WhenRecentForceRequestDowngradesToNonForce()
    {
        var now = DateTime.UtcNow;
        var last = now.AddMilliseconds(-4);
        var decision = ZOrderRequestBurstDedupPolicy.Resolve(
            lastRequestUtc: last,
            lastForceEnforceZOrder: true,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: ZOrderRequestBurstDedupDefaults.MinIntervalMs);

        decision.ShouldQueue.Should().BeFalse();
        decision.LastRequestUtc.Should().Be(last);
        decision.LastForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.DedupWeakerAfterForceWithinWindow);
    }

    [Fact]
    public void Resolve_ShouldQueueWithOutsideWindowReason_WhenOutsideDedupWindow()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestBurstDedupPolicy.Resolve(
            lastRequestUtc: now.AddMilliseconds(-120),
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: 30);

        decision.ShouldQueue.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.QueuedOutsideDedupWindow);
    }
}
