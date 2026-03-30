using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldUpdateTrackedState()
    {
        var state = LauncherBubbleVisibleChangedRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 9, 0, 0, DateTimeKind.Utc);
        var decision = new LauncherBubbleVisibleChangedDedupDecision(
            ShouldApply: true,
            Reason: LauncherBubbleVisibleChangedDedupReason.None,
            LastVisibleState: true,
            LastEventUtc: nowUtc);

        LauncherBubbleVisibleChangedStateUpdater.Apply(ref state, decision);

        state.LastVisibleState.Should().BeTrue();
        state.LastEventUtc.Should().Be(nowUtc);
    }
}
