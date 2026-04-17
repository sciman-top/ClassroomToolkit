using FluentAssertions;

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
}
