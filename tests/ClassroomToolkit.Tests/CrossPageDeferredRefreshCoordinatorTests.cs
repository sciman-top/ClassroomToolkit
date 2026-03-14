using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDeferredRefreshCoordinatorTests
{
    [Fact]
    public async Task ScheduleAsync_ShouldSkip_WhenGateBlocksBeforeSchedule()
    {
        var events = new List<string>();

        var result = await CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: false,
            delayOverrideMs: null,
            configuredDelayMs: 120,
            lastPointerUpUtc: DateTime.UtcNow,
            getCurrentUtcTimestamp: () => DateTime.UtcNow,
            isCrossPageDisplayActive: static () => false,
            isCrossPageInteractionActive: static () => false,
            tryAcquirePostInputRefreshSlot: (out long seq) =>
            {
                seq = 0;
                return true;
            },
            requestCrossPageDisplayUpdate: static _ => throw new Xunit.Sdk.XunitException("should not request"),
            tryBeginInvoke: static (_, _) => throw new Xunit.Sdk.XunitException("should not begin invoke"),
            delayAsync: static _ => Task.CompletedTask,
            incrementRefreshToken: static () => 1,
            readRefreshToken: static () => 1,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: (action, source, detail) => events.Add($"{action}:{source}:{detail}"));

        result.SkippedBeforeSchedule.Should().BeTrue();
        events.Should().ContainSingle().Which.Should().StartWith("defer-skip:");
    }

    [Fact]
    public async Task ScheduleAsync_ShouldRequestImmediateRefresh_WhenDelayAlreadyElapsed()
    {
        string? requested = null;

        var result = await CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: false,
            delayOverrideMs: null,
            configuredDelayMs: 120,
            lastPointerUpUtc: DateTime.UtcNow.AddMilliseconds(-500),
            getCurrentUtcTimestamp: () => DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            isCrossPageInteractionActive: static () => false,
            tryAcquirePostInputRefreshSlot: (out long seq) =>
            {
                seq = 0;
                return true;
            },
            requestCrossPageDisplayUpdate: source => requested = source,
            tryBeginInvoke: static (_, _) => true,
            delayAsync: static _ => Task.CompletedTask,
            incrementRefreshToken: static () => 1,
            readRefreshToken: static () => 1,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: static (_, _, _) => { });

        result.RequestedImmediateRefresh.Should().BeTrue();
        requested.Should().Be(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PostInput));
    }

    [Fact]
    public async Task ScheduleAsync_ShouldSkipImmediateRefresh_WhenSinglePerPointerUpSlotAlreadyUsed()
    {
        var events = new List<string>();

        var result = await CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: true,
            delayOverrideMs: null,
            configuredDelayMs: 120,
            lastPointerUpUtc: DateTime.UtcNow.AddMilliseconds(-500),
            getCurrentUtcTimestamp: () => DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            isCrossPageInteractionActive: static () => false,
            tryAcquirePostInputRefreshSlot: (out long seq) =>
            {
                seq = 7;
                return false;
            },
            requestCrossPageDisplayUpdate: static _ => throw new Xunit.Sdk.XunitException("should not request"),
            tryBeginInvoke: static (_, _) => true,
            delayAsync: static _ => Task.CompletedTask,
            incrementRefreshToken: static () => 1,
            readRefreshToken: static () => 1,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: (action, source, detail) => events.Add($"{action}:{source}:{detail}"));

        result.RequestedImmediateRefresh.Should().BeFalse();
        events.Should().Contain(e => e.Contains("already-refreshed seq=7"));
    }

    [Fact]
    public async Task ScheduleAsync_ShouldRequestDelayedRefresh_WhenDelayCompletesAndDispatchSucceeds()
    {
        string? requested = null;

        var result = await CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: false,
            delayOverrideMs: 200,
            configuredDelayMs: 120,
            lastPointerUpUtc: DateTime.UtcNow,
            getCurrentUtcTimestamp: () => DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            isCrossPageInteractionActive: static () => false,
            tryAcquirePostInputRefreshSlot: (out long seq) =>
            {
                seq = 0;
                return true;
            },
            requestCrossPageDisplayUpdate: source => requested = source,
            tryBeginInvoke: (action, _) =>
            {
                action();
                return true;
            },
            delayAsync: static _ => Task.CompletedTask,
            incrementRefreshToken: static () => 3,
            readRefreshToken: static () => 3,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: static (_, _, _) => { });

        result.ScheduledDelayedRefresh.Should().BeTrue();
        result.RequestedDelayedRefresh.Should().BeTrue();
        requested.Should().Be(CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.PostInput));
    }

    [Fact]
    public async Task ScheduleAsync_ShouldRecoverInline_WhenDelayThrowsAndRecoveryCannotBeScheduled()
    {
        string? requested = null;
        var events = new List<string>();

        var result = await CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: CrossPageUpdateSources.PostInput,
            singlePerPointerUp: false,
            delayOverrideMs: 200,
            configuredDelayMs: 120,
            lastPointerUpUtc: DateTime.UtcNow,
            getCurrentUtcTimestamp: () => DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            isCrossPageInteractionActive: static () => false,
            tryAcquirePostInputRefreshSlot: (out long seq) =>
            {
                seq = 0;
                return true;
            },
            requestCrossPageDisplayUpdate: source => requested = source,
            tryBeginInvoke: static (_, _) => false,
            delayAsync: static _ => Task.FromException(new InvalidOperationException("boom")),
            incrementRefreshToken: static () => 5,
            readRefreshToken: static () => 5,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: (action, source, detail) => events.Add($"{action}:{source}:{detail}"));

        result.RecoveredInlineAfterFailure.Should().BeTrue();
        requested.Should().Be(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PostInput));
        events.Should().Contain(e => e.StartsWith("defer-recover:"));
    }
}
