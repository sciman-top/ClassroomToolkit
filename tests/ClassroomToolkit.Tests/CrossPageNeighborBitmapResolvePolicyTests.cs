using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborBitmapResolvePolicyTests
{
    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnFalse_WhenInteractionActiveAndSlotChanged()
    {
        var allowed = CrossPageNeighborBitmapResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: true,
            slotPageChanged: true);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_WhenInteractionInactive()
    {
        var allowed = CrossPageNeighborBitmapResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: false,
            slotPageChanged: true);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_WhenSlotNotChanged()
    {
        var allowed = CrossPageNeighborBitmapResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: true,
            slotPageChanged: false);

        allowed.Should().BeTrue();
    }
}
