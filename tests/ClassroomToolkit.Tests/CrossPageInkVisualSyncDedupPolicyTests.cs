using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInkVisualSyncDedupPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldReturnTrue_ForRedrawCompletedImmediatelyAfterStateChanged()
    {
        var shouldSkip = CrossPageInkVisualSyncDedupPolicy.ShouldSkip(
            CrossPageInkVisualSyncTrigger.InkRedrawCompleted,
            CrossPageInkVisualSyncTrigger.InkStateChanged,
            interactionActive: false,
            elapsedSinceLastMs: 20,
            duplicateWindowMs: CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForRedrawCompletedAfterWindow()
    {
        var shouldSkip = CrossPageInkVisualSyncDedupPolicy.ShouldSkip(
            CrossPageInkVisualSyncTrigger.InkRedrawCompleted,
            CrossPageInkVisualSyncTrigger.InkStateChanged,
            interactionActive: false,
            elapsedSinceLastMs: 120,
            duplicateWindowMs: CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenTriggerIsInkStateChanged()
    {
        var shouldSkip = CrossPageInkVisualSyncDedupPolicy.ShouldSkip(
            CrossPageInkVisualSyncTrigger.InkStateChanged,
            CrossPageInkVisualSyncTrigger.InkStateChanged,
            interactionActive: false,
            elapsedSinceLastMs: 10,
            duplicateWindowMs: CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenInteractionIsActive()
    {
        var shouldSkip = CrossPageInkVisualSyncDedupPolicy.ShouldSkip(
            CrossPageInkVisualSyncTrigger.InkRedrawCompleted,
            CrossPageInkVisualSyncTrigger.InkStateChanged,
            interactionActive: true,
            elapsedSinceLastMs: 20,
            duplicateWindowMs: CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs);

        shouldSkip.Should().BeFalse();
    }
}
