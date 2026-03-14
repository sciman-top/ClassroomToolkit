using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchExecuteAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldAllow_WhenApplyQueued()
    {
        var decision = FloatingDispatchExecuteAdmissionPolicy.Resolve(applyQueued: true);

        decision.ShouldExecute.Should().BeTrue();
        decision.Reason.Should().Be(FloatingDispatchExecuteAdmissionReason.ApplyQueued);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenApplyNotQueued()
    {
        var decision = FloatingDispatchExecuteAdmissionPolicy.Resolve(applyQueued: false);

        decision.ShouldExecute.Should().BeFalse();
        decision.Reason.Should().Be(FloatingDispatchExecuteAdmissionReason.NotQueued);
    }
}
