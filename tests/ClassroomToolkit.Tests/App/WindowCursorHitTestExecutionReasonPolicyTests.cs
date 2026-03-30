using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowCursorHitTestExecutionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "invalid-window-handle")]
    [InlineData(2, "cursor-unavailable")]
    [InlineData(3, "window-rect-unavailable")]
    [InlineData(4, "hit-test-completed")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        WindowCursorHitTestExecutionReasonPolicy.ResolveTag((WindowCursorHitTestExecutionReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
