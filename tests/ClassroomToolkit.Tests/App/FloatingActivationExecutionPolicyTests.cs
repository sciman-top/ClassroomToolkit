using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingActivationExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTargetMissing_WhenTargetIsNull()
    {
        var decision = FloatingActivationExecutionPolicy.Resolve<string>(null, shouldActivate: true);
        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(FloatingActivationExecutionReason.TargetMissing);
    }

    [Fact]
    public void Resolve_ShouldReturnActivationNotRequested_WhenFlagIsFalse()
    {
        var decision = FloatingActivationExecutionPolicy.Resolve("overlay", shouldActivate: false);
        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(FloatingActivationExecutionReason.ActivationNotRequested);
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenTargetExistsAndFlagTrue()
    {
        var decision = FloatingActivationExecutionPolicy.Resolve("overlay", shouldActivate: true);
        decision.ShouldActivate.Should().BeTrue();
        decision.Reason.Should().Be(FloatingActivationExecutionReason.None);
    }

    [Fact]
    public void ShouldActivate_ShouldMapResolveDecision()
    {
        FloatingActivationExecutionPolicy.ShouldActivate("overlay", shouldActivate: true)
            .Should()
            .BeTrue();
    }
}
