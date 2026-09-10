using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayWindowLifecycleContractTests
{
    [Fact]
    public void LifecycleTimers_ShouldExitEarly_WhenOverlayIsClosing()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("private bool ShouldIgnoreLifecycleTick()");
        source.Should().Contain("if (ShouldIgnoreLifecycleTick())");
        source.Should().Contain("Volatile.Read(ref _overlayClosed) != 0 || _overlayLifecycleCancellation.IsCancellationRequested");
        source.Should().Contain("_presentationFocusMonitor.Stop();");
        source.Should().Contain("_inkMonitor.Stop();");
    }

    [Fact]
    public void VisibilityChanges_ShouldRefreshNativeInputOwnershipBeforeAsyncHookRefresh()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Lifecycle.cs"));
        var handlerStart = source.IndexOf(
            "private void OnOverlayVisibleChanged(",
            StringComparison.Ordinal);
        handlerStart.Should().BeGreaterThanOrEqualTo(0);
        var handlerEnd = source.IndexOf(
            "private void OnOverlaySourceInitialized(",
            handlerStart,
            StringComparison.Ordinal);
        handlerEnd.Should().BeGreaterThan(handlerStart);

        var handler = source[handlerStart..handlerEnd];
        var refreshIndex = handler.IndexOf("RefreshPresentationInputOwnership();", StringComparison.Ordinal);
        var asyncRefreshIndex = handler.IndexOf("UpdateWpsNavHookState();", StringComparison.Ordinal);
        refreshIndex.Should().BeGreaterThanOrEqualTo(0);
        asyncRefreshIndex.Should().BeGreaterThan(refreshIndex);
    }

    [Fact]
    public void HiddenOverlay_ShouldClearPointerCaptureBeforeRefreshingHookState()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Lifecycle.cs"));
        var handlerStart = source.IndexOf(
            "private void OnOverlayVisibleChanged(",
            StringComparison.Ordinal);
        var handlerEnd = source.IndexOf(
            "private void OnOverlaySourceInitialized(",
            handlerStart,
            StringComparison.Ordinal);

        handlerStart.Should().BeGreaterThanOrEqualTo(0);
        handlerEnd.Should().BeGreaterThan(handlerStart);
        var handler = source[handlerStart..handlerEnd];
        var hiddenCleanupIndex = handler.IndexOf(
            "HandlePointerCaptureLoss(\"overlay-hidden\");",
            StringComparison.Ordinal);
        var ownershipRefreshIndex = handler.IndexOf(
            "RefreshPresentationInputOwnership();",
            StringComparison.Ordinal);

        hiddenCleanupIndex.Should().BeGreaterThanOrEqualTo(0);
        ownershipRefreshIndex.Should().BeGreaterThan(hiddenCleanupIndex);
    }
}
