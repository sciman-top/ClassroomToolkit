using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageVisualSyncDuplicateWindowIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldIncreaseForUndo()
    {
        CrossPageVisualSyncDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.UndoSnapshot,
            defaultMs: 12).Should().Be(CrossPageVisualSyncDuplicateWindowIntervalThresholds.UndoMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForRegionErase()
    {
        CrossPageVisualSyncDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.RegionEraseCrossPage,
            defaultMs: 12).Should().Be(CrossPageVisualSyncDuplicateWindowIntervalThresholds.RegionEraseMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForReplaySources()
    {
        CrossPageVisualSyncDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.InkVisualSyncReplay,
            defaultMs: 12).Should().Be(CrossPageVisualSyncDuplicateWindowIntervalThresholds.ReplayMs);
    }
}
