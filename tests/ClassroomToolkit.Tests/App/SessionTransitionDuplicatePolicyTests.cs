using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDuplicatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnAdvanced_WhenCurrentIdIsGreater()
    {
        var decision = SessionTransitionDuplicatePolicy.Resolve(
            lastAppliedTransitionId: 10,
            currentTransitionId: 11);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionDuplicateReason.TransitionAdvanced);
    }

    [Fact]
    public void Resolve_ShouldReturnDuplicate_WhenCurrentIdEqualsLast()
    {
        var decision = SessionTransitionDuplicatePolicy.Resolve(
            lastAppliedTransitionId: 10,
            currentTransitionId: 10);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateReason.DuplicateTransitionId);
    }

    [Fact]
    public void Resolve_ShouldReturnRegressed_WhenCurrentIdLessThanLast()
    {
        var decision = SessionTransitionDuplicatePolicy.Resolve(
            lastAppliedTransitionId: 10,
            currentTransitionId: 9);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateReason.RegressedTransitionId);
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        SessionTransitionDuplicatePolicy.ShouldApply(
            lastAppliedTransitionId: 10,
            currentTransitionId: 11).Should().BeTrue();
    }
}
