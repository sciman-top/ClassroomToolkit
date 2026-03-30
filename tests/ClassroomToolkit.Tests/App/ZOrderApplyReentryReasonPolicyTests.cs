using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderApplyReentryReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "not-applying")]
    [InlineData(2, "forced-during-applying")]
    [InlineData(3, "follow-up-slot-available")]
    [InlineData(4, "applying-and-queued")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        ZOrderApplyReentryReasonPolicy.ResolveTag((ZOrderApplyReentryReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
