using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldAccept_WhenNotApplying()
    {
        var decision = ToolbarInteractionRetouchAdmissionPolicy.Resolve(
            zOrderApplying: false,
            applyQueued: false,
            forceEnforceZOrder: false);

        decision.ShouldRequest.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchAdmissionReason.None);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenApplyingAndQueuedWithoutForce()
    {
        var decision = ToolbarInteractionRetouchAdmissionPolicy.Resolve(
            zOrderApplying: true,
            applyQueued: true,
            forceEnforceZOrder: false);

        decision.ShouldRequest.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchAdmissionReason.ReentryBlocked);
    }

    [Fact]
    public void Resolve_ShouldAccept_WhenForceEnabled()
    {
        var decision = ToolbarInteractionRetouchAdmissionPolicy.Resolve(
            zOrderApplying: true,
            applyQueued: true,
            forceEnforceZOrder: true);

        decision.ShouldRequest.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchAdmissionReason.None);
    }

    [Fact]
    public void ShouldRequest_ShouldMapResolveDecision()
    {
        ToolbarInteractionRetouchAdmissionPolicy.ShouldRequest(
            zOrderApplying: false,
            applyQueued: false,
            forceEnforceZOrder: false).Should().BeTrue();
    }
}
