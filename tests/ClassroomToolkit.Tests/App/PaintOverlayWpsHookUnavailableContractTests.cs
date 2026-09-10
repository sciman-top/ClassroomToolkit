using AwesomeAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintOverlayWpsHookUnavailableContractTests
{
    [Fact]
    public void PaintOverlayPresentation_ShouldUseAtomicWpsHookUnavailableNotificationGate()
    {
        var source = GetSource();

        source.Should().Contain("WpsHookUnavailableNotificationPolicy.ShouldNotify(ref _wpsHookUnavailableNotifiedState)");
    }

    [Fact]
    public void PaintOverlayPresentation_ShouldResetWpsHookUnavailableGate_OnRecoveryAndModeReset()
    {
        var source = GetSource();

        source.Should().Contain("WpsHookUnavailableNotificationPolicy.Reset(ref _wpsHookUnavailableNotifiedState);");
    }

    [Fact]
    public void PaintOverlayPresentation_ShouldFallbackInline_WhenWpsHookDispatchSchedulingFailsOnUiThread()
    {
        var source = GetSource();

        source.Should().Contain("var scheduled = TryBeginInvoke(ExecuteHookRequest, System.Windows.Threading.DispatcherPriority.Normal);");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("ExecuteHookRequest();");
        source.Should().Contain("var scheduled = TryBeginInvoke(ShowUnavailableMessage, System.Windows.Threading.DispatcherPriority.Background);");
        source.Should().Contain("ShowUnavailableMessage();");
    }

    [Fact]
    public void PaintOverlayPresentation_ShouldRecheckLifecycleAndHookAdmission_WhenQueuedRequestRuns()
    {
        var source = GetSource();
        var handlerStart = source.IndexOf("void ExecuteHookRequest()", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf(
            "var scheduled = TryBeginInvoke(ExecuteHookRequest",
            handlerStart,
            StringComparison.Ordinal);

        handlerStart.Should().BeGreaterThanOrEqualTo(0);
        handlerEnd.Should().BeGreaterThan(handlerStart);

        var handler = source[handlerStart..handlerEnd];
        handler.Should().Contain("ShouldIgnoreLifecycleTick()");
        handler.Should().Contain("WpsHookEnableGatePolicy.ShouldAttemptResolveTarget(");
        handler.IndexOf("WpsHookEnableGatePolicy.ShouldAttemptResolveTarget(", StringComparison.Ordinal)
            .Should().BeLessThan(handler.IndexOf("ResolveWpsTarget()", StringComparison.Ordinal));
    }

    private static string GetSource()
    {
        var paintRoot = TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(paintRoot, "PaintOverlayWindow.Presentation*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
