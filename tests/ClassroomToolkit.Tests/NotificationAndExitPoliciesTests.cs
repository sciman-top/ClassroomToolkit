using AwesomeAssertions;
using ClassroomToolkit.App;
using System.Collections.Concurrent;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class InkStartupCleanupLogPolicyTests
{
    [Fact]
    public void ShouldLogDeletionSummary_ShouldReturnTrue_WhenAnyCountPositive()
    {
        var summary = new InkStartupCleanupSummary(TotalSidecars: 1, TotalComposites: 0);

        NotificationAndExitPolicies.ShouldLogDeletionSummary(summary).Should().BeTrue();
    }

    [Fact]
    public void ShouldLogDeletionSummary_ShouldReturnFalse_WhenCountsAreZero()
    {
        var summary = new InkStartupCleanupSummary(TotalSidecars: 0, TotalComposites: 0);

        NotificationAndExitPolicies.ShouldLogDeletionSummary(summary).Should().BeFalse();
    }

    [Fact]
    public void FormatDeletionSummary_ShouldContainCounts()
    {
        var summary = new InkStartupCleanupSummary(TotalSidecars: 3, TotalComposites: 2);

        var message = NotificationAndExitPolicies.FormatDeletionSummary(summary);

        message.Should().Contain("sidecars=3");
        message.Should().Contain("composites=2");
    }
}

public sealed class RemoteHookUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrue_OnlyOnFirstCall()
    {
        var state = 0;

        var first = NotificationAndExitPolicies.ShouldNotifyRemoteHookUnavailableNotification(ref state);
        var second = NotificationAndExitPolicies.ShouldNotifyRemoteHookUnavailableNotification(ref state);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotify_ShouldAllowOnlyOneWinner_UnderConcurrency()
    {
        var state = 0;
        var results = new ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                results.Add(NotificationAndExitPolicies.ShouldNotifyRemoteHookUnavailableNotification(ref state));
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        results.Count(result => result).Should().Be(1);
        results.Count(result => !result).Should().Be(31);
    }

    [Fact]
    public void Reset_ShouldAllowNotifyAgain()
    {
        var state = 0;
        NotificationAndExitPolicies.ShouldNotifyRemoteHookUnavailableNotification(ref state).Should().BeTrue();
        NotificationAndExitPolicies.ShouldNotifyRemoteHookUnavailableNotification(ref state).Should().BeFalse();

        NotificationAndExitPolicies.Reset(ref state);

        NotificationAndExitPolicies.ShouldNotifyRemoteHookUnavailableNotification(ref state).Should().BeTrue();
    }
}

public sealed class RollCallClickSuppressionPolicyTests
{
    [Fact]
    public void ExtendSuppressUntil_ShouldUseLaterTimestamp()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var current = nowUtc.AddMilliseconds(300);

        var result = NotificationAndExitPolicies.ExtendSuppressUntil(
            current,
            nowUtc,
            TimeSpan.FromMilliseconds(120));

        result.Should().Be(current);
    }

    [Fact]
    public void ExtendSuppressUntil_ShouldExtend_WhenDurationIsLonger()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var current = nowUtc.AddMilliseconds(60);

        var result = NotificationAndExitPolicies.ExtendSuppressUntil(
            current,
            nowUtc,
            TimeSpan.FromMilliseconds(240));

        result.Should().Be(nowUtc.AddMilliseconds(240));
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenWithinWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var suppressUntilUtc = nowUtc.AddMilliseconds(1);

        var suppressed = NotificationAndExitPolicies.ShouldSuppress(suppressUntilUtc, nowUtc);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenExpired()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var suppressUntilUtc = nowUtc.AddMilliseconds(-1);

        var suppressed = NotificationAndExitPolicies.ShouldSuppress(suppressUntilUtc, nowUtc);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ExtendSuppressUntil_ShouldIgnoreNonPositiveDuration()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var current = nowUtc.AddMilliseconds(100);

        var result = NotificationAndExitPolicies.ExtendSuppressUntil(
            current,
            nowUtc,
            TimeSpan.Zero);

        result.Should().Be(current);
    }
}

public sealed class SettingsSaveFailureNotificationPolicyTests
{
    [Fact]
    public void Resolve_ShouldNotify_WhenNotPreviouslyNotified()
    {
        var plan = NotificationAndExitPolicies.Resolve(alreadyNotified: false);

        plan.ShouldNotify.Should().BeTrue();
        plan.NextNotifiedState.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotNotify_WhenAlreadyNotified()
    {
        var plan = NotificationAndExitPolicies.Resolve(alreadyNotified: true);

        plan.ShouldNotify.Should().BeFalse();
        plan.NextNotifiedState.Should().BeTrue();
    }
}

public sealed class SpeechUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrue_OnlyOnFirstCall()
    {
        var state = 0;

        var first = NotificationAndExitPolicies.ShouldNotifySpeechUnavailableNotification(ref state);
        var second = NotificationAndExitPolicies.ShouldNotifySpeechUnavailableNotification(ref state);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotify_ShouldAllowOnlyOneWinner_UnderConcurrency()
    {
        var state = 0;
        var results = new ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                results.Add(NotificationAndExitPolicies.ShouldNotifySpeechUnavailableNotification(ref state));
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        results.Count(result => result).Should().Be(1);
        results.Count(result => !result).Should().Be(31);
    }
}
