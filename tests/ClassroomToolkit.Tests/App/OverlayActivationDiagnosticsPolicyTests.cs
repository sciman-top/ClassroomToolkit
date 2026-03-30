using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationDiagnosticsPolicyTests
{
    [Fact]
    public void FormatRetouchSkipMessage_ShouldIncludeReasonTag()
    {
        var message = OverlayActivationDiagnosticsPolicy.FormatRetouchSkipMessage(
            OverlayActivationRetouchReason.Throttled);

        message.Should().Be("[OverlayActivation][Retouch] skip reason=throttled");
    }

    [Fact]
    public void FormatSuppressionMessage_ShouldIncludeReasonTag()
    {
        var message = OverlayActivationDiagnosticsPolicy.FormatSuppressionMessage(
            OverlayActivationSuppressionReason.SuppressionRequested);

        message.Should().Be("[OverlayActivation][Suppression] apply reason=suppression-requested");
    }
}
