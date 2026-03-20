using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintWindowOrchestratorEventCallbackSafetyContractTests
{
    [Fact]
    public void ForwardedOverlayAndToolbarEvents_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("RaiseEventSafely(() => PhotoModeChanged?.Invoke(active), nameof(PhotoModeChanged));");
        source.Should().Contain("RaiseEventSafely(() => PhotoNavigationRequested?.Invoke(direction), nameof(PhotoNavigationRequested));");
        source.Should().Contain("RaiseEventSafely(() => SettingsRequested?.Invoke(), nameof(SettingsRequested));");
        source.Should().Contain("RaiseEventSafely(() => PhotoOpenRequested?.Invoke(), nameof(PhotoOpenRequested));");
        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("PaintWindowOrchestrator event callback failed");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Services",
            "PaintWindowOrchestrator.cs");
    }
}
