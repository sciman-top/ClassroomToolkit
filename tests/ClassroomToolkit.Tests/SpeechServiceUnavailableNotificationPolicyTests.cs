using System.Threading;
using ClassroomToolkit.Services.Speech;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SpeechServiceUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrueOnlyOnce_WithoutReset()
    {
        var state = 0;

        var first = SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref state);
        var second = SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref state);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotify_ShouldAllowSingleWinner_UnderConcurrency()
    {
        var state = 0;
        var trueCount = 0;

        await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                if (SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref state))
                {
                    Interlocked.Increment(ref trueCount);
                }
            })));

        trueCount.Should().Be(1);
    }

    [Fact]
    public void Reset_ShouldRearmNotificationGate()
    {
        var state = 0;

        SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeTrue();
        SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeFalse();

        SpeechServiceUnavailableNotificationPolicy.Reset(ref state);

        SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeTrue();
    }
}
