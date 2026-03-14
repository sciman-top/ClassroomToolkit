using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ExplicitForegroundRetouchStateUpdaterTests
{
    [Fact]
    public void MarkRetouched_ShouldUpdateLastRetouchUtc()
    {
        var state = ExplicitForegroundRetouchRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc);

        ExplicitForegroundRetouchStateUpdater.MarkRetouched(ref state, nowUtc);

        state.LastRetouchUtc.Should().Be(nowUtc);
    }
}
