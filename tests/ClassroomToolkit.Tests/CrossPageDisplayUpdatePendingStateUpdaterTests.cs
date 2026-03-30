using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public class CrossPageDisplayUpdatePendingStateUpdaterTests
{
    [Fact]
    public void MarkDelayedScheduled_RuntimeState_ShouldSetPendingAndIncrementToken()
    {
        var nowUtc = DateTime.UtcNow;
        var state = CrossPageDisplayUpdateRuntimeState.Default;

        var next = CrossPageDisplayUpdatePendingStateUpdater.MarkDelayedScheduled(ref state, nowUtc);

        state.Pending.Should().BeTrue();
        state.Token.Should().Be(1);
        state.PendingSinceUtc.Should().Be(nowUtc);
        next.Should().Be(1);
    }

    [Fact]
    public void MarkDirectScheduled_RuntimeState_ShouldRefreshPendingSince()
    {
        var first = DateTime.UtcNow.AddMilliseconds(-50);
        var nowUtc = DateTime.UtcNow;
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 3,
            PendingSinceUtc: first);

        CrossPageDisplayUpdatePendingStateUpdater.MarkDirectScheduled(ref state, nowUtc);

        state.PendingSinceUtc.Should().Be(nowUtc);
    }

    [Fact]
    public void MarkDelayedScheduled_RuntimeState_ShouldRefreshPendingSince_WhenAlreadyPending()
    {
        var first = DateTime.UtcNow.AddMilliseconds(-50);
        var nowUtc = DateTime.UtcNow;
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 3,
            PendingSinceUtc: first);

        var next = CrossPageDisplayUpdatePendingStateUpdater.MarkDelayedScheduled(ref state, nowUtc);

        state.Token.Should().Be(4);
        state.PendingSinceUtc.Should().Be(nowUtc);
        next.Should().Be(4);
    }

    [Fact]
    public void MarkPendingCleared_RuntimeState_ShouldClearPendingSince()
    {
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 3,
            PendingSinceUtc: DateTime.UtcNow);

        CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref state);

        state.Pending.Should().BeFalse();
        state.PendingSinceUtc.Should().Be(CrossPageRuntimeDefaults.UnsetTimestampUtc);
    }

    [Fact]
    public void IsTokenMatched_RuntimeState_ShouldReturnExpected()
    {
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 3,
            PendingSinceUtc: DateTime.UtcNow);

        CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(state, 3).Should().BeTrue();
        CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(state, 2).Should().BeFalse();
    }

    [Fact]
    public void MarkDelayedScheduled_ShouldSetPendingAndIncrementToken()
    {
        var pending = false;
        var token = 0;

        var next = CrossPageDisplayUpdatePendingStateUpdater.MarkDelayedScheduled(ref pending, ref token);

        pending.Should().BeTrue();
        token.Should().Be(1);
        next.Should().Be(1);
    }

    [Fact]
    public void MarkDirectScheduled_ShouldSetPending()
    {
        var pending = false;

        CrossPageDisplayUpdatePendingStateUpdater.MarkDirectScheduled(ref pending);

        pending.Should().BeTrue();
    }

    [Fact]
    public void MarkPendingCleared_ShouldResetPending()
    {
        var pending = true;

        CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref pending);

        pending.Should().BeFalse();
    }
}
