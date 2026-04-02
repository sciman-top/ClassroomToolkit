using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkRenderAdmissionPolicyTests
{
    [Fact]
    public void ShouldRejectStaleCacheKey_ShouldReturnTrue_WhenExpectedKeyIsEmpty()
    {
        CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(
            cacheKey: "doc|1",
            expectedCacheKey: string.Empty).Should().BeTrue();
    }

    [Fact]
    public void ShouldRejectStaleCacheKey_ShouldReturnTrue_WhenCacheKeyDoesNotMatchExpected()
    {
        CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(
            cacheKey: "docA|1",
            expectedCacheKey: "docB|1").Should().BeTrue();
    }

    [Fact]
    public void ShouldRejectStaleCacheKey_ShouldReturnFalse_WhenCacheKeyMatchesExpectedExactly()
    {
        CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(
            cacheKey: "doc|1",
            expectedCacheKey: "doc|1").Should().BeFalse();
    }
}
