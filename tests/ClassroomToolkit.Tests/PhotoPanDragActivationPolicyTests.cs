using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanDragActivationPolicyTests
{
    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnFalse_WhenCrossPageInactive()
    {
        PhotoPanDragActivationPolicy.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: false,
            deltaYDip: 20).Should().BeFalse();
    }

    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnFalse_WhenDeltaWithinThreshold()
    {
        PhotoPanDragActivationPolicy.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: true,
            deltaYDip: PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip).Should().BeFalse();
    }

    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnTrue_WhenDeltaExceedsThreshold()
    {
        PhotoPanDragActivationPolicy.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: true,
            deltaYDip: PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip + 0.1).Should().BeTrue();
    }

    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnTrue_WhenNegativeDeltaExceedsThreshold()
    {
        PhotoPanDragActivationPolicy.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: true,
            deltaYDip: -(PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip + 0.1)).Should().BeTrue();
    }
}
