using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestReentryReasonPolicyTests
{
    [Theory]
    [InlineData((int)ZOrderApplyReentryReason.ApplyingAndQueued, (int)ZOrderRequestAdmissionReason.ReentryApplyingAndQueued)]
    [InlineData((int)ZOrderApplyReentryReason.NotApplying, (int)ZOrderRequestAdmissionReason.ReentryBlocked)]
    [InlineData((int)ZOrderApplyReentryReason.ForcedDuringApplying, (int)ZOrderRequestAdmissionReason.ReentryBlocked)]
    [InlineData((int)ZOrderApplyReentryReason.FollowUpSlotAvailable, (int)ZOrderRequestAdmissionReason.ReentryBlocked)]
    public void ResolveAdmissionReason_ShouldReturnExpectedReason(int reentryReasonValue, int expectedAdmissionReasonValue)
    {
        var reentryReason = (ZOrderApplyReentryReason)reentryReasonValue;
        var expected = (ZOrderRequestAdmissionReason)expectedAdmissionReasonValue;

        ZOrderRequestReentryReasonPolicy.ResolveAdmissionReason(reentryReason)
            .Should()
            .Be(expected);
    }
}
