using ClassroomToolkit.App;
using FluentAssertions;
using System.Collections.Concurrent;

namespace ClassroomToolkit.Tests.App;

public sealed class RemoteHookUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrue_OnlyOnFirstCall()
    {
        var state = 0;

        var first = RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref state);
        var second = RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref state);

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
                results.Add(RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref state));
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
        RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeTrue();
        RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeFalse();

        RemoteHookUnavailableNotificationPolicy.Reset(ref state);

        RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeTrue();
    }
}
