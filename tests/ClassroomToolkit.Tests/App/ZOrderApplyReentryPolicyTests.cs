using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderApplyReentryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNotApplying_WhenNotApplying()
    {
        var decision = ZOrderApplyReentryPolicy.Resolve(
            zOrderApplying: false,
            applyQueued: false,
            forceEnforceZOrder: false);

        decision.ShouldAcceptRequest.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderApplyReentryReason.NotApplying);
    }

    [Fact]
    public void Resolve_ShouldReturnApplyingAndQueued_WhenApplyingAndAlreadyQueuedAndNotForce()
    {
        var decision = ZOrderApplyReentryPolicy.Resolve(
            zOrderApplying: true,
            applyQueued: true,
            forceEnforceZOrder: false);

        decision.ShouldAcceptRequest.Should().BeFalse();
        decision.Reason.Should().Be(ZOrderApplyReentryReason.ApplyingAndQueued);
    }

    [Fact]
    public void Resolve_ShouldReturnForcedDuringApplying_WhenApplyingAndForce()
    {
        var decision = ZOrderApplyReentryPolicy.Resolve(
            zOrderApplying: true,
            applyQueued: true,
            forceEnforceZOrder: true);

        decision.ShouldAcceptRequest.Should().BeTrue();
        decision.Reason.Should().Be(ZOrderApplyReentryReason.ForcedDuringApplying);
    }

    [Fact]
    public void ShouldAcceptRequest_ShouldMapResolveDecision()
    {
        ZOrderApplyReentryPolicy.ShouldAcceptRequest(
            zOrderApplying: true,
            applyQueued: false,
            forceEnforceZOrder: false).Should().BeTrue();
    }
}
