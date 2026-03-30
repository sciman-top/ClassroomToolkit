using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairAdmissionReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldMapKnownReasons()
    {
        ToolbarInteractionDirectRepairAdmissionReasonPolicy.ResolveTag(
            ToolbarInteractionDirectRepairAdmissionReason.ZOrderApplying).Should().Be("zorder-applying");
        ToolbarInteractionDirectRepairAdmissionReasonPolicy.ResolveTag(
            ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued).Should().Be("zorder-queued");
    }

    [Fact]
    public void ResolveTag_ShouldFallbackToNone()
    {
        ToolbarInteractionDirectRepairAdmissionReasonPolicy.ResolveTag(
            ToolbarInteractionDirectRepairAdmissionReason.None).Should().Be("none");
    }
}
