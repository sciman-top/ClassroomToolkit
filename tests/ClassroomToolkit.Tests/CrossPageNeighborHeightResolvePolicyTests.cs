using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborHeightResolvePolicyTests
{
    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnFalse_ForImageDuringInteraction()
    {
        var allowed = CrossPageNeighborHeightResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: true,
            photoDocumentIsPdf: false);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_ForImageWhenNotInteracting()
    {
        var allowed = CrossPageNeighborHeightResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: false,
            photoDocumentIsPdf: false);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_ForPdfDuringInteraction()
    {
        var allowed = CrossPageNeighborHeightResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: true,
            photoDocumentIsPdf: true);

        allowed.Should().BeTrue();
    }
}
