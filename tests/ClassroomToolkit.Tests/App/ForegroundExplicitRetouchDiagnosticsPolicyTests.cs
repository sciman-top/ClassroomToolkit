using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundExplicitRetouchDiagnosticsPolicyTests
{
    [Fact]
    public void FormatThrottleSkipMessage_ShouldIncludeSurfaceAndReason()
    {
        var message = ForegroundExplicitRetouchDiagnosticsPolicy.FormatThrottleSkipMessage(
            ZOrderSurface.PhotoFullscreen,
            ForegroundExplicitRetouchThrottleReason.Throttled);

        message.Should().Be("[ExplicitForeground][Throttle] skip surface=PhotoFullscreen reason=throttled");
    }
}
