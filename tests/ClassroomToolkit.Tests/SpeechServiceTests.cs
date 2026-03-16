using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassroomToolkit.Services.Speech;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SpeechServiceTests
{
    [Fact]
    public async Task SpeakAsync_InvalidVoice_ShouldNotifyUnavailableOnlyOnce()
    {
        var service = new SpeechService();
        var unavailableCount = 0;
        service.SpeechUnavailable += () => Interlocked.Increment(ref unavailableCount);

        try
        {
            var invalidVoiceId = $"invalid-voice-{Guid.NewGuid():N}";
            await service.SpeakAsync("first", invalidVoiceId);
            await service.SpeakAsync("second", invalidVoiceId);

            unavailableCount.Should().Be(1);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SpeakAsync_AfterDispose_ShouldNotThrow()
    {
        var service = new SpeechService();
        service.Dispose();

        var act = async () => await service.SpeakAsync("disposed-call", "any");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SpeakAsync_ConcurrentCalls_ShouldNotThrow()
    {
        var service = new SpeechService();

        try
        {
            var invalidVoiceId = $"invalid-voice-{Guid.NewGuid():N}";
            var tasks = Enumerable
                .Range(0, 20)
                .Select(i => service.SpeakAsync($"name-{i}", invalidVoiceId));

            var act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SpeakAsync_UnavailableCallbackThrowsRecoverable_ShouldNotThrow()
    {
        var service = new SpeechService();
        service.SpeechUnavailable += () => throw new InvalidOperationException("callback-boom");

        try
        {
            var invalidVoiceId = $"invalid-voice-{Guid.NewGuid():N}";
            var act = async () => await service.SpeakAsync("recoverable-callback", invalidVoiceId);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SpeakAsync_RecoverableCallbackFailure_ShouldNotBlockOtherSubscribers()
    {
        var service = new SpeechService();
        var callbackCount = 0;
        service.SpeechUnavailable += () => throw new InvalidOperationException("callback-boom");
        service.SpeechUnavailable += () => Interlocked.Increment(ref callbackCount);

        try
        {
            var invalidVoiceId = $"invalid-voice-{Guid.NewGuid():N}";
            await service.SpeakAsync("recoverable-callback", invalidVoiceId);

            callbackCount.Should().Be(1);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SpeakAsync_UnavailableCallbackThrowsFatal_ShouldRethrow()
    {
        var service = new SpeechService();
        service.SpeechUnavailable += () => throw new BadImageFormatException("fatal-callback");

        try
        {
            var invalidVoiceId = $"invalid-voice-{Guid.NewGuid():N}";
            var act = async () => await service.SpeakAsync("fatal-callback", invalidVoiceId);

            await act.Should().ThrowAsync<BadImageFormatException>();
        }
        finally
        {
            service.Dispose();
        }
    }
}
