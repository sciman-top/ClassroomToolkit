using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestAdmissionDecisionFactoryTests
{
    [Fact]
    public void Reject_ShouldPreservePreviousStateAndDisableQueue()
    {
        var lastUtc = new DateTime(2026, 3, 7, 7, 0, 0, DateTimeKind.Utc);

        var decision = ZOrderRequestAdmissionDecisionFactory.Reject(lastUtc, true);

        decision.ShouldQueue.Should().BeFalse();
        decision.LastRequestUtc.Should().Be(lastUtc);
        decision.LastForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.ReentryBlocked);
    }

    [Fact]
    public void FromDedup_ShouldMapFields()
    {
        var nowUtc = new DateTime(2026, 3, 7, 7, 0, 0, DateTimeKind.Utc);
        var dedup = new ZOrderRequestBurstDedupDecision(
            ShouldQueue: true,
            LastForceEnforceZOrder: false,
            LastRequestUtc: nowUtc,
            Reason: ZOrderRequestAdmissionReason.QueuedOutsideDedupWindow);

        var decision = ZOrderRequestAdmissionDecisionFactory.FromDedup(dedup);

        decision.ShouldQueue.Should().BeTrue();
        decision.LastRequestUtc.Should().Be(nowUtc);
        decision.LastForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.QueuedOutsideDedupWindow);
    }

    [Fact]
    public void Reject_ShouldUseProvidedReason_WhenSpecified()
    {
        var lastUtc = new DateTime(2026, 3, 7, 8, 0, 0, DateTimeKind.Utc);

        var decision = ZOrderRequestAdmissionDecisionFactory.Reject(
            lastUtc,
            lastForceEnforceZOrder: false,
            reason: ZOrderRequestAdmissionReason.ReentryApplyingAndQueued);

        decision.ShouldQueue.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderRequestAdmissionReason.ReentryApplyingAndQueued);
    }
}
