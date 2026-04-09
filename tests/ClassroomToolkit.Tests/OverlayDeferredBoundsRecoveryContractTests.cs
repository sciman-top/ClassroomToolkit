using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayDeferredBoundsRecoveryContractTests
{
    [Fact]
    public void RequestDeferredFullscreenBoundsRecovery_ShouldFallbackInline_WhenDispatchUnavailable()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("var scheduled = TryBeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ApplyFullscreenBounds();");
    }
}
