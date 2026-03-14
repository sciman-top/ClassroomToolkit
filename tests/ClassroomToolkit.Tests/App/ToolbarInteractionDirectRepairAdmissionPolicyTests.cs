using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenZOrderApplying()
    {
        var decision = ToolbarInteractionDirectRepairAdmissionPolicy.Resolve(
            zOrderApplying: true,
            zOrderQueued: false);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairAdmissionReason.ZOrderApplying);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenZOrderQueued()
    {
        var decision = ToolbarInteractionDirectRepairAdmissionPolicy.Resolve(
            zOrderApplying: false,
            zOrderQueued: true);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued);
    }

    [Fact]
    public void Resolve_ShouldAllow_WhenNoZOrderPressure()
    {
        var decision = ToolbarInteractionDirectRepairAdmissionPolicy.Resolve(
            zOrderApplying: false,
            zOrderQueued: false);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairAdmissionReason.None);
    }
}
