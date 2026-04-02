using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallWindowLifecycleSubscriptionContractTests
{
    [Fact]
    public void RollCallWindow_ShouldSubscribePaintModeEvents_WithNamedHandlers()
    {
        var source = File.ReadAllText(GetSourcePath("RollCallWindow.xaml.cs"));

        source.Should().Contain("PaintModeManager.Instance.PaintModeChanged += OnPaintModeChanged;");
        source.Should().Contain("PaintModeManager.Instance.IsDrawingChanged += OnDrawingStateChanged;");
        source.Should().Contain("_viewModel.GroupButtons.CollectionChanged += OnGroupButtonsCollectionChanged;");
        source.Should().Contain("_windowBoundsSaveTimer.Tick += OnWindowBoundsSaveTick;");
        source.Should().Contain("_hoverCheckTimer.Tick += OnHoverCheckTimerTick;");
        source.Should().Contain("SizeChanged += OnWindowSizeChanged;");
        source.Should().Contain("LocationChanged += OnWindowLocationChanged;");
        source.Should().Contain("SourceInitialized += OnSourceInitialized;");
        source.Should().Contain("IsVisibleChanged += OnWindowVisibilityChanged;");
        source.Should().NotContain("SourceInitialized += (_, _) =>");
        source.Should().NotContain("IsVisibleChanged += (_, _) =>");
    }

    [Fact]
    public void RollCallWindow_ShouldUnsubscribeExternalEvents_OnClosing()
    {
        var source = File.ReadAllText(GetSourcePath("RollCallWindow.Windowing.cs"));

        source.Should().Contain("PaintModeManager.Instance.PaintModeChanged -= OnPaintModeChanged;");
        source.Should().Contain("PaintModeManager.Instance.IsDrawingChanged -= OnDrawingStateChanged;");
        source.Should().Contain("_speechService.SpeechUnavailable -= OnSpeechUnavailable;");
        source.Should().Contain("if (_closingCleanupStarted)");
        source.Should().Contain("_closingCleanupStarted = true;");
        source.Should().Contain("_lifecycleCancellation.Cancel();");
        source.Should().Contain("_remoteHookStartGate.Dispose();");
        source.Should().Contain("_hoverCheckTimer.Stop();");
        source.Should().Contain("_windowBoundsSaveTimer.Tick -= OnWindowBoundsSaveTick;");
        source.Should().Contain("_hoverCheckTimer.Tick -= OnHoverCheckTimerTick;");
        source.Should().Contain("SizeChanged -= OnWindowSizeChanged;");
        source.Should().Contain("LocationChanged -= OnWindowLocationChanged;");
        source.Should().Contain("SourceInitialized -= OnSourceInitialized;");
        source.Should().Contain("IsVisibleChanged -= OnWindowVisibilityChanged;");
        source.Should().Contain("_groupOverlay.Closed -= OnGroupOverlayClosed;");
        source.Should().Contain("overlay.Closed -= OnGroupOverlayClosed;");
        source.Should().Contain("_timer.Tick -= OnTimerTick;");
        source.Should().Contain("_rollStateSaveTimer.Tick -= OnRollStateSaveTick;");
        source.Should().Contain("_viewModel.GroupButtons.CollectionChanged -= OnGroupButtonsCollectionChanged;");
        source.Should().Contain("_viewModel.TimerCompleted -= OnTimerCompleted;");
        source.Should().Contain("_viewModel.ReminderTriggered -= OnReminderTriggered;");
        source.Should().Contain("_viewModel.DataLoadFailed -= OnDataLoadFailed;");
        source.Should().Contain("_viewModel.DataSaveFailed -= OnDataSaveFailed;");
        source.Should().Contain("_photoResolver?.Dispose();");
        source.Should().Contain("_photoResolver = null;");
        source.Should().Contain("_viewModel.Dispose();");
        source.Should().Contain("_lifecycleCancellation.Dispose();");
        source.Should().NotContain("_groupOverlay.Closed += (s, e) => _groupOverlay = null;");
    }

    [Fact]
    public void RollCallWindow_ShouldDifferentiateMinimizeAndCloseActions()
    {
        var source = File.ReadAllText(GetSourcePath("RollCallWindow.Windowing.cs"));

        source.Should().Contain("private void OnMinimizeClick(object sender, RoutedEventArgs e)");
        source.Should().Contain("HideRollCall();");
        source.Should().Contain("private void OnCloseClick(object sender, RoutedEventArgs e)");
        source.Should().Contain("RequestClose();");
    }

    private static string GetSourcePath(string fileName)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            fileName);
    }
}
