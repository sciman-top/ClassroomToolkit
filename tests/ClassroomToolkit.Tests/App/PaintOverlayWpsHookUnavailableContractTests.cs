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

    [Fact]
    public void PaintOverlayPresentation_ShouldFallbackInline_WhenWpsHookDispatchSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var scheduled = TryBeginInvoke(ExecuteHookRequest, System.Windows.Threading.DispatcherPriority.Background);");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("ExecuteHookRequest();");
        source.Should().Contain("var scheduled = TryBeginInvoke(ShowUnavailableMessage, System.Windows.Threading.DispatcherPriority.Background);");
        source.Should().Contain("ShowUnavailableMessage();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
    }
}
