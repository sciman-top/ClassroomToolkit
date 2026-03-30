using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStateNormalizationDiagnosticsPolicyTests
{
    [Fact]
    public void FormatResolveMessage_ShouldContainReasonTag()
    {
        var message = WindowStateNormalizationDiagnosticsPolicy.FormatResolveMessage(
            WindowStateNormalizationReason.NormalizationRequested);

        message.Should().Contain("[WindowStateNormalization]");
        message.Should().Contain("reason=normalization-requested");
    }
}
