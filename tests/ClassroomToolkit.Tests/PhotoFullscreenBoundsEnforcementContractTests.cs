using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoFullscreenBoundsEnforcementContractTests
{
    [Fact]
    public void SchedulePhotoFullscreenBoundsEnforcement_ShouldHandleDispatchFailureFallback()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation*.cs");

        source.Should().Contain("var scheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("fullscreen-enforcement dispatch unavailable.");
    }
}
