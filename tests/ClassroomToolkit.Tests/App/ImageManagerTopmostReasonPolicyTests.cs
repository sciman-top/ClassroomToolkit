using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerTopmostReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "image-manager-hidden")]
    [InlineData(2, "front-surface-mismatch")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        ImageManagerTopmostReasonPolicy.ResolveTag((ImageManagerTopmostReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
