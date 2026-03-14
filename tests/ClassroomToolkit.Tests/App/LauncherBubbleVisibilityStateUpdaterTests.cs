using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibilityStateUpdaterTests
{
    [Fact]
    public void MarkVisibleChangedSuppressionCooldown_ShouldSetFutureTimestamp()
    {
        var state = LauncherBubbleVisibilityRuntimeState.Default;
        var nowUtc = DateTime.UtcNow;

        LauncherBubbleVisibilityStateUpdater.MarkVisibleChangedSuppressionCooldown(
            ref state,
            nowUtc,
            cooldownMs: 100);

        state.SuppressVisibleChangedUntilUtc.Should().BeAfter(nowUtc);
    }

    [Fact]
    public void MarkVisibleChangedSuppressionCooldown_ShouldClearTimestamp_WhenCooldownIsZero()
    {
        var state = LauncherBubbleVisibilityRuntimeState.Default;

        LauncherBubbleVisibilityStateUpdater.MarkVisibleChangedSuppressionCooldown(
            ref state,
            DateTime.UtcNow,
            cooldownMs: 0);

        state.SuppressVisibleChangedUntilUtc.Should().Be(WindowDedupDefaults.UnsetTimestampUtc);
    }
}
