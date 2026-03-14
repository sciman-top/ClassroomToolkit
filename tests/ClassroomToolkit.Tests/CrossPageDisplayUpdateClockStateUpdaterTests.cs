using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateClockStateUpdaterTests
{
    [Fact]
    public void MarkUpdated_ShouldSetLastUpdateUtc()
    {
        var state = CrossPageDisplayUpdateClockState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 16, 30, 0, DateTimeKind.Utc);

        CrossPageDisplayUpdateClockStateUpdater.MarkUpdated(ref state, nowUtc);

        state.LastUpdateUtc.Should().Be(nowUtc);
    }
}
