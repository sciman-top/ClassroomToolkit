using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowExitLifecycleContractTests
{
    [Fact]
    public void RequestExit_ShouldCloseImageManagerWindow_WhenPresent()
    {
        var source = MainWindowContractSourceReader.ReadCombinedSource();

        source.Should().Contain("hasImageManagerWindow: _imageManagerWindow != null");
        source.Should().Contain("if (exitPlan.ShouldCloseImageManagerWindow && _imageManagerWindow != null)");
        source.Should().Contain("close-image-manager-window");
        source.Should().Contain("imageManagerWindow.Close");
    }

    [Fact]
    public void LifecycleTimers_ShouldExitEarly_WhenShutdownHasStarted()
    {
        var source = MainWindowContractSourceReader.ReadCombinedSource();

        source.Should().Contain("private bool ShouldIgnoreShutdownTicks()");
        source.Should().Contain("if (ShouldIgnoreShutdownTicks())");
        source.Should().Contain("_backgroundTasksCancellationDisposed || _backgroundTasksCancellation.IsCancellationRequested");
        source.Should().Contain("_floatingTopmostWatchdogTimer.Stop();");
    }

    [Fact]
    public void RequestExit_ShouldKeepWindowOpen_WhenFinalSettingsSaveFails()
    {
        var source = MainWindowContractSourceReader.ReadCombinedSource();

        source.Should().Contain("CapturePaintToolbarPosition(save: false)");
        source.Should().Contain("if (!SaveLauncherSettings())");
        source.Should().Contain("return;");
        source.Should().Contain("_allowClose = true;");
    }
}
