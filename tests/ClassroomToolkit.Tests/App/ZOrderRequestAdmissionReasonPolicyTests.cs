using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestAdmissionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "queued")]
    [InlineData(1, "reentry-blocked")]
    [InlineData(2, "dedup-same-force")]
    [InlineData(3, "dedup-weaker-after-force")]
    [InlineData(4, "queued-no-history")]
    [InlineData(5, "queued-dedup-disabled")]
    [InlineData(6, "queued-force-escalation")]
    [InlineData(7, "queued-outside-window")]
    [InlineData(8, "reentry-applying-and-queued")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        var reason = (ZOrderRequestAdmissionReason)reasonValue;
        ZOrderRequestAdmissionReasonPolicy.ResolveTag(reason).Should().Be(expectedTag);
    }
}
