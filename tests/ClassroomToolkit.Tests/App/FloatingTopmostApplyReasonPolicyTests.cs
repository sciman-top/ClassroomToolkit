using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostApplyReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "force-requested")]
    [InlineData(2, "missing-last-state")]
    [InlineData(3, "front-surface-changed")]
    [InlineData(4, "topmost-plan-changed")]
    [InlineData(5, "unchanged")]
    [InlineData(6, "launcher-interactive-retouch")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingTopmostApplyReasonPolicy.ResolveTag((FloatingTopmostApplyPolicy.FloatingTopmostApplyReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
