using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SurfaceZOrderDecisionDedupReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "no-history")]
    [InlineData(2, "interval-disabled")]
    [InlineData(3, "unset-timestamp")]
    [InlineData(4, "skipped-within-window")]
    [InlineData(5, "applied")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        SurfaceZOrderDecisionDedupReasonPolicy.ResolveTag((SurfaceZOrderDecisionDedupReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
