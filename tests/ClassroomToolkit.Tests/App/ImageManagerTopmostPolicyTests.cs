using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerTopmostPolicyTests
{
    [Theory]
    [InlineData(true, ZOrderSurface.ImageManager, true)]
    [InlineData(true, ZOrderSurface.PhotoFullscreen, false)]
    [InlineData(false, ZOrderSurface.ImageManager, false)]
    public void Resolve_ShouldMatchExpected(bool visible, ZOrderSurface surface, bool expected)
    {
        var decision = ImageManagerTopmostPolicy.Resolve(visible, surface);
        decision.ShouldApply.Should().Be(expected);
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        ImageManagerTopmostPolicy.ShouldApply(true, ZOrderSurface.ImageManager).Should().BeTrue();
    }
}
