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

    [Fact]
    public void SpeechService_Diagnostics_ShouldIncludeExceptionType()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private static string FormatDiagnostic(string operation, Exception ex)");
        source.Should().Contain("return $\"[SpeechService] {operation} failed: {ex.GetType().Name} - {ex.Message}\";");
        source.Should().Contain("Debug.WriteLine(FormatDiagnostic(\"Speak\", failure));");
        source.Should().Contain("Debug.WriteLine(FormatDiagnostic(\"SpeechUnavailable callback\", callbackEx));");
        source.Should().Contain("Debug.WriteLine(FormatDiagnostic(\"Cancel pending speech\", ex));");
        source.Should().Contain("Debug.WriteLine(FormatDiagnostic(\"Dispose\", ex));");
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
