using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsNavigationDebounceStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldUpdateDebounceState()
    {
        (int Code, IntPtr Target, DateTime Timestamp)? last = null;
        var blockUntilUtc = PresentationRuntimeDefaults.UnsetTimestampUtc;
        var nowUtc = new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc),
            BlockUntilUtc: nowUtc.AddMilliseconds(200));

        WpsNavigationDebounceStateUpdater.Apply(
            ref last,
            ref blockUntilUtc,
            state);

        last.Should().Be((1, (IntPtr)123, nowUtc));
        blockUntilUtc.Should().Be(nowUtc.AddMilliseconds(200));
    }
}
