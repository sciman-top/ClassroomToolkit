using ClassroomToolkit.App;
using FluentAssertions;
using System.Collections.Concurrent;

namespace ClassroomToolkit.Tests.App;

public sealed class SpeechUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrue_OnlyOnFirstCall()
    {
        var state = 0;

        var first = SpeechUnavailableNotificationPolicy.ShouldNotify(ref state);
        var second = SpeechUnavailableNotificationPolicy.ShouldNotify(ref state);

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
                results.Add(SpeechUnavailableNotificationPolicy.ShouldNotify(ref state));
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        results.Count(result => result).Should().Be(1);
        results.Count(result => !result).Should().Be(31);
    }
}
