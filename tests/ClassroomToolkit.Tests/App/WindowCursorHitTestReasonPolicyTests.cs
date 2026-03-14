using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowCursorHitTestReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "inside-bounds")]
    [InlineData(2, "outside-bounds")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        WindowCursorHitTestReasonPolicy.ResolveTag((WindowCursorHitTestReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
