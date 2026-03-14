using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairDispatchAdmissionReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldMapKnownReason()
    {
        ToolbarInteractionDirectRepairDispatchAdmissionReasonPolicy.ResolveTag(
            ToolbarInteractionDirectRepairDispatchAdmissionReason.AlreadyQueued).Should().Be("already-queued");
    }

    [Fact]
    public void ResolveTag_ShouldFallbackToNone()
    {
        ToolbarInteractionDirectRepairDispatchAdmissionReasonPolicy.ResolveTag(
            ToolbarInteractionDirectRepairDispatchAdmissionReason.None).Should().Be("none");
    }
}
