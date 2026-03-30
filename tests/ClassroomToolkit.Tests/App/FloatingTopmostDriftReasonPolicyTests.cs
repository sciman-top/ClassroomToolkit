using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostDriftReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "toolbar-drift")]
    [InlineData(2, "rollcall-drift")]
    [InlineData(3, "launcher-drift")]
    [InlineData(4, "no-drift")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingTopmostDriftReasonPolicy.ResolveTag((FloatingTopmostDriftReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
