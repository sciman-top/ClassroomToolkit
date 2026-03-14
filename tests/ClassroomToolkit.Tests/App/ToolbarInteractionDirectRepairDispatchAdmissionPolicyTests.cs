using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairDispatchAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenAlreadyQueued()
    {
        var decision = ToolbarInteractionDirectRepairDispatchAdmissionPolicy.Resolve(alreadyQueued: true);

        decision.ShouldDispatch.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairDispatchAdmissionReason.AlreadyQueued);
    }

    [Fact]
    public void Resolve_ShouldAllow_WhenNotQueued()
    {
        var decision = ToolbarInteractionDirectRepairDispatchAdmissionPolicy.Resolve(alreadyQueued: false);

        decision.ShouldDispatch.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairDispatchAdmissionReason.None);
    }
}
