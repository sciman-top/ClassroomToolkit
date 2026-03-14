using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDuplicateResetPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnResetRequired_WhenOverlayRewiredAndLastIdExists()
    {
        var decision = SessionTransitionDuplicateResetPolicy.Resolve(
            overlayWindowRewired: true,
            lastAppliedTransitionId: 12);

        decision.ShouldReset.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionDuplicateResetReason.ResetRequired);
    }

    [Fact]
    public void Resolve_ShouldReturnOverlayNotRewired_WhenOverlayNotRewired()
    {
        var decision = SessionTransitionDuplicateResetPolicy.Resolve(
            overlayWindowRewired: false,
            lastAppliedTransitionId: 12);

        decision.ShouldReset.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateResetReason.OverlayNotRewired);
    }

    [Fact]
    public void Resolve_ShouldReturnNoAppliedTransition_WhenNoLastId()
    {
        var decision = SessionTransitionDuplicateResetPolicy.Resolve(
            overlayWindowRewired: true,
            lastAppliedTransitionId: 0);

        decision.ShouldReset.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateResetReason.NoAppliedTransition);
    }

    [Fact]
    public void ShouldReset_ShouldMapResolveDecision()
    {
        SessionTransitionDuplicateResetPolicy.ShouldReset(
            overlayWindowRewired: true,
            lastAppliedTransitionId: 12).Should().BeTrue();
    }
}
