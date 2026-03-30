using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivatedRetouchStateUpdaterTests
{
    [Fact]
    public void MarkSuppressNextApply_ThenConsume_ShouldReturnTrueAndClearFlag()
    {
        var state = OverlayActivatedRetouchRuntimeState.Default;

        OverlayActivatedRetouchStateUpdater.MarkSuppressNextApply(ref state);
        var consumed = OverlayActivatedRetouchStateUpdater.TryConsumeSuppression(ref state);

        consumed.Should().BeTrue();
        state.SuppressNextApply.Should().BeFalse();
    }

    [Fact]
    public void MarkRetouched_ShouldUpdateLastRetouchUtc()
    {
        var state = OverlayActivatedRetouchRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 14, 30, 0, DateTimeKind.Utc);

        OverlayActivatedRetouchStateUpdater.MarkRetouched(ref state, nowUtc);

        state.LastRetouchUtc.Should().Be(nowUtc);
    }
}
