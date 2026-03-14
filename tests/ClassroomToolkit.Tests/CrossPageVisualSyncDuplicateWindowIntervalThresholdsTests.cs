using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageVisualSyncDuplicateWindowIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPageVisualSyncDuplicateWindowIntervalThresholds.UndoMs.Should().Be(24);
        CrossPageVisualSyncDuplicateWindowIntervalThresholds.RegionEraseMs.Should().Be(20);
        CrossPageVisualSyncDuplicateWindowIntervalThresholds.InkRedrawCompletedMs.Should().Be(18);
        CrossPageVisualSyncDuplicateWindowIntervalThresholds.InkStateChangedMs.Should().Be(14);
        CrossPageVisualSyncDuplicateWindowIntervalThresholds.ReplayMs.Should().Be(22);
    }
}
