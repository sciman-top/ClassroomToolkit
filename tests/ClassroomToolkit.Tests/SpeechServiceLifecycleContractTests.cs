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
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.Services",
            "Speech",
            "SpeechService.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
