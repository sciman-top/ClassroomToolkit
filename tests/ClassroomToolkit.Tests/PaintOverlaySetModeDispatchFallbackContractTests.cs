using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlaySetModeDispatchFallbackContractTests
{
    [Fact]
    public void SetMode_ShouldFallbackInline_WhenDispatcherSchedulingFails()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("var cursorUpdateScheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!cursorUpdateScheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("var modeFollowUpScheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!modeFollowUpScheduled && Dispatcher.CheckAccess())");
    }
}
