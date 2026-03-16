using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageMissingNeighborRefreshCoordinatorTests
{
    [Fact]
    public async Task ScheduleAsync_ShouldSkip_WhenPolicyRejectsSchedule()
    {
        var result = await CrossPageMissingNeighborRefreshCoordinator.ScheduleAsync(
            missingCount: 0,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            lastScheduledUtc: DateTime.UtcNow,
            nowUtc: DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            updateLastScheduledUtc: static _ => throw new Xunit.Sdk.XunitException("should not update"),
            requestCrossPageDisplayUpdate: static _ => throw new Xunit.Sdk.XunitException("should not request"),
            tryBeginInvoke: static (_, _) => throw new Xunit.Sdk.XunitException("should not invoke"),
            delayAsync: static _ => Task.CompletedTask,
            incrementRefreshToken: static () => 1,
            readRefreshToken: static () => 1,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: static (_, _, _) => { });

        result.Scheduled.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleAsync_ShouldRequestDelayedRefresh_WhenDelayCompletesAndInvokeRuns()
    {
        string? requested = null;
        DateTime updatedUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;

        var result = await CrossPageMissingNeighborRefreshCoordinator.ScheduleAsync(
            missingCount: 2,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            updateLastScheduledUtc: value => updatedUtc = value,
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

        result.Scheduled.Should().BeTrue();
        result.RequestedDelayedRefresh.Should().BeTrue();
        updatedUtc.Should().NotBe(CrossPageRuntimeDefaults.UnsetTimestampUtc);
        requested.Should().Be(CrossPageUpdateSources.NeighborMissingDelayed);
    }

    [Fact]
    public async Task ScheduleAsync_ShouldRecoverInline_WhenDelayThrowsAndRecoveryCannotBeScheduled()
    {
        string? requested = null;

        var result = await CrossPageMissingNeighborRefreshCoordinator.ScheduleAsync(
            missingCount: 2,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            updateLastScheduledUtc: static _ => { },
            requestCrossPageDisplayUpdate: source => requested = source,
            tryBeginInvoke: static (_, _) => false,
            delayAsync: static _ => Task.FromException(new InvalidOperationException("boom")),
            incrementRefreshToken: static () => 5,
            readRefreshToken: static () => 5,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: static (_, _, _) => { });

        result.RecoveredInlineAfterFailure.Should().BeTrue();
        requested.Should().Be(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.NeighborMissingDelayed));
    }

    [Fact]
    public async Task ScheduleAsync_ShouldAbort_WhenDispatchFailsAndCannotRecoverInline()
    {
        var events = new List<string>();

        var result = await CrossPageMissingNeighborRefreshCoordinator.ScheduleAsync(
            missingCount: 2,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            updateLastScheduledUtc: static _ => { },
            requestCrossPageDisplayUpdate: static _ => { },
            tryBeginInvoke: static (_, _) => false,
            delayAsync: static _ => Task.CompletedTask,
            incrementRefreshToken: static () => 7,
            readRefreshToken: static () => 7,
            dispatcherCheckAccess: static () => false,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: (action, source, detail) => events.Add($"{action}:{source}:{detail}"));

        result.RecoveredInlineAfterFailure.Should().BeFalse();
        events.Should().Contain(e => e.Contains("missing-neighbor-delayed-dispatch-failed"));
    }

    [Fact]
    public async Task ScheduleAsync_ShouldRethrowFatal_WhenDelayThrowsFatalException()
    {
        var act = async () => await CrossPageMissingNeighborRefreshCoordinator.ScheduleAsync(
            missingCount: 2,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            isCrossPageDisplayActive: static () => true,
            updateLastScheduledUtc: static _ => { },
            requestCrossPageDisplayUpdate: static _ => { },
            tryBeginInvoke: static (_, _) => false,
            delayAsync: static _ => Task.FromException(new BadImageFormatException("fatal-delay")),
            incrementRefreshToken: static () => 5,
            readRefreshToken: static () => 5,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false,
            diagnostics: static (_, _, _) => { });

        await act.Should().ThrowAsync<BadImageFormatException>();
    }
}
