using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayDiagnosticsPolicyTests
{
    [Fact]
    public void FormatMessage_ShouldContainOverlayCategoryAndEventName()
    {
        var message = ClassroomToolkit.App.Photos.PhotoOverlayDiagnosticsPolicy
            .FormatMessage("show-start", "req=12 path=a.jpg");

        message.Should().Contain("[PhotoOverlay][show-start]");
        message.Should().Contain("req=12");
        message.Should().Contain("path=a.jpg");
    }

    [Fact]
    public void LatestLogFileName_ShouldUseStableLatestPath()
    {
        ClassroomToolkit.App.Photos.PhotoOverlayDiagnostics.LatestLogFileName
            .Should().Be("photo-overlay-latest.log");
    }
}
