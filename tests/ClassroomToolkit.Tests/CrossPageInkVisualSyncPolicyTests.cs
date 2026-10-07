using ClassroomToolkit.App.Paint;
using AwesomeAssertions;
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

    [Theory]
    [InlineData(true, true, false, 20, true)]
    [InlineData(true, true, false, 64, false)]
    [InlineData(true, true, false, -1, false)]
    [InlineData(true, true, true, 20, false)]
    [InlineData(false, true, false, 20, false)]
    [InlineData(true, false, false, 20, false)]
    public void ShouldSkipDuplicateRedraw_ShouldRequireRecentStateChangeAndIdleInteraction(
        bool redrawCompleted,
        bool lastWasStateChanged,
        bool interactionActive,
        double elapsedSinceLastMs,
        bool shouldSkip)
    {
        var trigger = redrawCompleted
            ? CrossPageInkVisualSyncTrigger.InkRedrawCompleted
            : CrossPageInkVisualSyncTrigger.InkStateChanged;
        var lastTrigger = lastWasStateChanged
            ? CrossPageInkVisualSyncTrigger.InkStateChanged
            : CrossPageInkVisualSyncTrigger.InkRedrawCompleted;
        var actual = CrossPageInkVisualSyncPolicy.ShouldSkipDuplicateRedraw(
            trigger,
            lastTrigger,
            interactionActive,
            elapsedSinceLastMs);

        actual.Should().Be(shouldSkip);
    }

    [Fact]
    public void ShouldSkipDuplicateRedraw_ShouldReturnFalseWithoutPreviousTrigger()
    {
        CrossPageInkVisualSyncPolicy.ShouldSkipDuplicateRedraw(
            CrossPageInkVisualSyncTrigger.InkRedrawCompleted,
            lastTrigger: null,
            interactionActive: false,
            elapsedSinceLastMs: 20).Should().BeFalse();
    }
}
