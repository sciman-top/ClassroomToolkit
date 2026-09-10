using ClassroomToolkit.App.Paint;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsNavigationDebounceStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldUpdateDebounceState()
    {
        (int Code, IntPtr Target, DateTime Timestamp)? last = null;
        var nowUtc = new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc));

        WpsNavigationDebounceStateUpdater.Apply(
            ref last,
            state);

        last.Should().Be((1, (IntPtr)123, nowUtc));
    }
}
