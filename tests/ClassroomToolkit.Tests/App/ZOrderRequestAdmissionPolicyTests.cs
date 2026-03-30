using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenReentryPolicyRejects()
    {
        var now = DateTime.UtcNow;
        var decision = ZOrderRequestAdmissionPolicy.Resolve(
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
        var decision = ZOrderRequestAdmissionPolicy.Resolve(
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
        var decision = ZOrderRequestAdmissionPolicy.Resolve(
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
