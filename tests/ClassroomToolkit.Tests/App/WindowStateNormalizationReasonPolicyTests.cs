using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStateNormalizationReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "target-missing")]
    [InlineData(2, "normalization-not-requested")]
    [InlineData(3, "normalization-requested")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        WindowStateNormalizationReasonPolicy.ResolveTag((WindowStateNormalizationReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
