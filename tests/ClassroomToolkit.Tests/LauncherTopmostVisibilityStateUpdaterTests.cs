using System;
using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class LauncherTopmostVisibilityStateUpdaterTests
{
    [Fact]
    public void ApplyResolvedTimestamp_ShouldUseNow_WhenLauncherIsVisibleForTopmost()
    {
        var lastVisibleUtc = new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc);
        var nowUtc = new DateTime(2026, 3, 11, 10, 5, 0, DateTimeKind.Utc);

        LauncherTopmostVisibilityStateUpdater.ApplyResolvedTimestamp(
            ref lastVisibleUtc,
            nowUtc,
            visibleForTopmost: true);

        lastVisibleUtc.Should().Be(nowUtc);
    }

    [Fact]
    public void ApplyResolvedTimestamp_ShouldKeepPrevious_WhenLauncherIsHiddenForTopmost()
    {
        var lastVisibleUtc = new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc);
        var nowUtc = new DateTime(2026, 3, 11, 10, 5, 0, DateTimeKind.Utc);

        LauncherTopmostVisibilityStateUpdater.ApplyResolvedTimestamp(
            ref lastVisibleUtc,
            nowUtc,
            visibleForTopmost: false);

        lastVisibleUtc.Should().Be(new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc));
    }
}
