using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionEventAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenStateUnchanged()
    {
        var decision = SessionTransitionEventAdmissionPolicy.Resolve(
            hasStateChange: false,
            lastAppliedTransitionId: 10,
            currentTransitionId: 11);

        decision.ShouldProcess.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.NoStateChange);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenTransitionIdNotAdvanced()
    {
        var decision = SessionTransitionEventAdmissionPolicy.Resolve(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 10);

        decision.ShouldProcess.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.DuplicateTransitionId);
    }

    [Fact]
    public void Resolve_ShouldRejectWithRegressedReason_WhenTransitionIdRegressed()
    {
        var decision = SessionTransitionEventAdmissionPolicy.Resolve(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 9);

        decision.ShouldProcess.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.RegressedTransitionId);
    }

    [Fact]
    public void Resolve_ShouldAccept_WhenStateChangedAndTransitionAdvanced()
    {
        var decision = SessionTransitionEventAdmissionPolicy.Resolve(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 11);

        decision.ShouldProcess.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.None);
    }

    [Fact]
    public void ShouldProcess_ShouldMapResolveDecision()
    {
        SessionTransitionEventAdmissionPolicy.ShouldProcess(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 11).Should().BeTrue();
    }
}
