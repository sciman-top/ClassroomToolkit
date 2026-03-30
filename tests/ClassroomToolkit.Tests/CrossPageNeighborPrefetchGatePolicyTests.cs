using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPrefetchGatePolicyTests
{
    [Fact]
    public void ShouldSchedule_ShouldReturnFalse_WhenNotPhotoModeOrPdf()
    {
        CrossPageNeighborPrefetchGatePolicy.ShouldSchedule(
            photoModeActive: false,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: true,
            interactionActive: false).Should().BeFalse();

        CrossPageNeighborPrefetchGatePolicy.ShouldSchedule(
            photoModeActive: true,
            photoDocumentIsPdf: true,
            crossPageDisplayEnabled: true,
            interactionActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldSchedule_ShouldBlock_WhenCrossPageDisabledAndInteractionActive()
    {
        var result = CrossPageNeighborPrefetchGatePolicy.ShouldSchedule(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: false,
            interactionActive: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRunPrefetch_ShouldRequireIdleImagePhotoMode()
    {
        CrossPageNeighborPrefetchGatePolicy.ShouldRunPrefetch(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: true,
            interactionActive: false).Should().BeTrue();

        CrossPageNeighborPrefetchGatePolicy.ShouldRunPrefetch(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: true,
            interactionActive: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldRunPrefetch_ShouldReturnFalse_WhenCrossPageDisplayDisabled()
    {
        var result = CrossPageNeighborPrefetchGatePolicy.ShouldRunPrefetch(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: false,
            interactionActive: false);

        result.Should().BeFalse();
    }
}
