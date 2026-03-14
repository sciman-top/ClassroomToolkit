using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class UserInitiatedWindowActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnActivationRequired_WhenWindowVisible_AndNotActive()
    {
        var decision = UserInitiatedWindowActivationPolicy.Resolve(
            windowVisible: true,
            windowActive: false);

        decision.ShouldActivateAfterShow.Should().BeTrue();
        decision.Reason.Should().Be(UserInitiatedWindowActivationReason.ActivationRequired);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowNotVisible_WhenWindowNotVisible()
    {
        var decision = UserInitiatedWindowActivationPolicy.Resolve(
            windowVisible: false,
            windowActive: false);

        decision.ShouldActivateAfterShow.Should().BeFalse();
        decision.Reason.Should().Be(UserInitiatedWindowActivationReason.WindowNotVisible);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowAlreadyActive_WhenWindowAlreadyActive()
    {
        var decision = UserInitiatedWindowActivationPolicy.Resolve(
            windowVisible: true,
            windowActive: true);

        decision.ShouldActivateAfterShow.Should().BeFalse();
        decision.Reason.Should().Be(UserInitiatedWindowActivationReason.WindowAlreadyActive);
    }

    [Fact]
    public void ShouldActivateAfterShow_ShouldMapResolveDecision()
    {
        var result = UserInitiatedWindowActivationPolicy.ShouldActivateAfterShow(
            windowVisible: true,
            windowActive: false);

        result.Should().BeTrue();
    }
}
