using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintOverlayWpsHookUnavailableContractTests
{
    [Fact]
    public void PaintOverlayPresentation_ShouldUseAtomicWpsHookUnavailableNotificationGate()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("WpsHookUnavailableNotificationPolicy.ShouldNotify(ref _wpsHookUnavailableNotifiedState)");
    }

    [Fact]
    public void PaintOverlayPresentation_ShouldResetWpsHookUnavailableGate_OnRecoveryAndModeReset()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("WpsHookUnavailableNotificationPolicy.Reset(ref _wpsHookUnavailableNotifiedState);");
    }

    private static string GetSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
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
