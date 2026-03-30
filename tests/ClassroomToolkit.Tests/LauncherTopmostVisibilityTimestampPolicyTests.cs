using ClassroomToolkit.App;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherTopmostVisibilityTimestampPolicyTests
{
    [Fact]
    public void ResolveLastVisibleUtc_ShouldUseNow_WhenVisibleForTopmost()
    {
        var previous = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 3, 10, 10, 5, 0, DateTimeKind.Utc);

        var resolved = LauncherTopmostVisibilityTimestampPolicy.ResolveLastVisibleUtc(
            previous,
            now,
            visibleForTopmost: true);

        resolved.Should().Be(now);
    }

    [Fact]
    public void ResolveLastVisibleUtc_ShouldKeepPrevious_WhenNotVisibleForTopmost()
    {
        var previous = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 3, 10, 10, 5, 0, DateTimeKind.Utc);

        var resolved = LauncherTopmostVisibilityTimestampPolicy.ResolveLastVisibleUtc(
            previous,
            now,
            visibleForTopmost: false);

        resolved.Should().Be(previous);
    }
}
