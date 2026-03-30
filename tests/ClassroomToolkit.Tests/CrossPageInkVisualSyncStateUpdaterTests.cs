using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInkVisualSyncStateUpdaterTests
{
    [Fact]
    public void MarkApplied_ShouldUpdateSyncState()
    {
        var state = CrossPageInkVisualSyncRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 15, 30, 0, DateTimeKind.Utc);

        CrossPageInkVisualSyncStateUpdater.MarkApplied(
            ref state,
            nowUtc,
            CrossPageInkVisualSyncTrigger.InkStateChanged);

        state.LastSyncUtc.Should().Be(nowUtc);
        state.LastTrigger.Should().Be(CrossPageInkVisualSyncTrigger.InkStateChanged);
    }
}
