using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInkVisualSyncPolicyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Resolve_ShouldDisableSync_WhenPhotoModeOrCrossPageDisabled(
        bool photoModeActive,
        bool crossPageDisplayEnabled)
    {
        var decision = CrossPageInkVisualSyncPolicy.Resolve(
            photoModeActive,
            crossPageDisplayEnabled,
            CrossPageInkVisualSyncTrigger.InkStateChanged);

        decision.ShouldPrimeVisibleNeighborSlots.Should().BeFalse();
        decision.ShouldRequestCrossPageUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldPrimeAndRequest_WhenInkStateChangedAndEnabled()
    {
        var decision = CrossPageInkVisualSyncPolicy.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            CrossPageInkVisualSyncTrigger.InkStateChanged);

        decision.ShouldPrimeVisibleNeighborSlots.Should().BeTrue();
        decision.ShouldRequestCrossPageUpdate.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldOnlyRequest_WhenRedrawCompletedAndEnabled()
    {
        var decision = CrossPageInkVisualSyncPolicy.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            CrossPageInkVisualSyncTrigger.InkRedrawCompleted);

        decision.ShouldPrimeVisibleNeighborSlots.Should().BeFalse();
        decision.ShouldRequestCrossPageUpdate.Should().BeTrue();
    }
}
