using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class ZOrderApplyReentryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNotApplying_WhenNotApplying()
    {
        var decision = ZOrderRequestPolicies.ResolveZOrderApplyReentry(
            zOrderApplying: false,
            applyQueued: false,
            forceEnforceZOrder: false);

        decision.ShouldAcceptRequest.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderApplyReentryReason.NotApplying);
    }

    [Fact]
    public void Resolve_ShouldReturnApplyingAndQueued_WhenApplyingAndAlreadyQueuedAndNotForce()
    {
        var decision = ZOrderRequestPolicies.ResolveZOrderApplyReentry(
            zOrderApplying: true,
            applyQueued: true,
            forceEnforceZOrder: false);

        decision.ShouldAcceptRequest.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderApplyReentryReason.ApplyingAndQueued);
    }

    [Fact]
    public void Resolve_ShouldReturnForcedDuringApplying_WhenApplyingAndForce()
    {
        var decision = ZOrderRequestPolicies.ResolveZOrderApplyReentry(
            zOrderApplying: true,
            applyQueued: true,
            forceEnforceZOrder: true);

        decision.ShouldAcceptRequest.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderApplyReentryReason.ForcedDuringApplying);
    }

    [Fact]
    public void ShouldAcceptRequest_ShouldMapResolveDecision()
    {
        ZOrderRequestPolicies.ShouldAcceptRequest(
            zOrderApplying: true,
            applyQueued: false,
            forceEnforceZOrder: false).Should().BeTrue();
    }
}

public sealed class ZOrderRequestAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenReentryPolicyRejects()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestPolicies.ResolveAdmission(
            zOrderApplying: true,
            applyQueued: true,
            lastRequestUtc: now.AddMilliseconds(-2),
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false);

        decision.ShouldQueue.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.ReentryApplyingAndQueued);
    }

    [Fact]
    public void Resolve_ShouldApplyBurstDedup_WhenReentryAllows()
    {
        var now = DateTime.UtcNow;
        var state = new ZOrderRequestRuntimeState(
            LastRequestUtc: now.AddMilliseconds(-2),
            LastForceEnforceZOrder: false);
        var decision = ZOrderRequestPolicies.ResolveAdmission(
            zOrderApplying: false,
            applyQueued: false,
            state,
            nowUtc: now,
            forceEnforceZOrder: false,
            dedupIntervalMs: 12);

        decision.ShouldQueue.Should().BeFalse();
        decision.LastForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.DedupSameForceWithinWindow);
    }

    [Fact]
    public void Resolve_ShouldQueue_WhenForceEscalatesWithinWindow()
    {
        var now = DateTime.UtcNow;
        var state = new ZOrderRequestRuntimeState(
            LastRequestUtc: now.AddMilliseconds(-3),
            LastForceEnforceZOrder: false);
        var decision = ZOrderRequestPolicies.ResolveAdmission(
            zOrderApplying: false,
            applyQueued: false,
            state,
            nowUtc: now,
            forceEnforceZOrder: true,
            dedupIntervalMs: 12);

        decision.ShouldQueue.Should().BeTrue();
        decision.LastForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.QueuedForceEscalationWithinWindow);
    }
}

public sealed class ZOrderRequestBurstDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldQueue_WhenNoPreviousRequest()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestPolicies.ResolveBurstDedup(
            lastRequestUtc: WindowDedupDefaults.UnsetTimestampUtc,
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: ZOrderRequestBurstThresholds.RequestDedupMs);

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
        var decision = ZOrderRequestPolicies.ResolveBurstDedup(
            lastRequestUtc: last,
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: ZOrderRequestBurstThresholds.RequestDedupMs);

        decision.ShouldQueue.Should().BeFalse();
        decision.LastRequestUtc.Should().Be(last);
        decision.LastForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.DedupSameForceWithinWindow);
    }

    [Fact]
    public void Resolve_ShouldQueue_WhenWithinWindowButForceFlagChanged()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestPolicies.ResolveBurstDedup(
            lastRequestUtc: now.AddMilliseconds(-5),
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: true,
            minIntervalMs: ZOrderRequestBurstThresholds.RequestDedupMs);

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
        var decision = ZOrderRequestPolicies.ResolveBurstDedup(
            lastRequestUtc: last,
            lastForceEnforceZOrder: true,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: ZOrderRequestBurstThresholds.RequestDedupMs);

        decision.ShouldQueue.Should().BeFalse();
        decision.LastRequestUtc.Should().Be(last);
        decision.LastForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.DedupWeakerAfterForceWithinWindow);
    }

    [Fact]
    public void Resolve_ShouldQueueWithOutsideWindowReason_WhenOutsideDedupWindow()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestPolicies.ResolveBurstDedup(
            lastRequestUtc: now.AddMilliseconds(-120),
            lastForceEnforceZOrder: false,
            nowUtc: now,
            forceEnforceZOrder: false,
            minIntervalMs: 30);

        decision.ShouldQueue.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.QueuedOutsideDedupWindow);
    }
}
