using System;
using System.Threading;
using ClassroomToolkit.Services.Speech;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SpeechServiceNotificationTests
{
    [Fact]
    public void NotifySpeechUnavailable_ShouldNotBlockOtherSubscribers_WhenRecoverableCallbackThrows()
    {
        using var service = new SpeechService();
        var callbackCount = 0;
        service.SpeechUnavailable += () => throw new InvalidOperationException("callback-boom");
        service.SpeechUnavailable += () => Interlocked.Increment(ref callbackCount);

        var act = () => service.NotifySpeechUnavailableForTest();

        act.Should().NotThrow();
        callbackCount.Should().Be(1);
    }

    [Fact]
    public void NotifySpeechUnavailable_ShouldRethrowFatalCallbackException()
    {
        using var service = new SpeechService();
        service.SpeechUnavailable += () => throw new BadImageFormatException("fatal-callback");

        var act = () => service.NotifySpeechUnavailableForTest();

        act.Should().Throw<BadImageFormatException>();
    }
}
