using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SpeechServiceLifecycleContractTests
{
    [Fact]
    public void SpeechService_ShouldRearmUnavailableNotification_OnSuccessfulSpeak()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SpeechServiceUnavailableNotificationPolicy.Reset(ref _unavailableNotifiedState);");
    }

    [Fact]
    public void SpeechService_ShouldUseAtomicNotificationGate_OnFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref _unavailableNotifiedState)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Services",
            "Speech",
            "SpeechService.cs");
    }
}
