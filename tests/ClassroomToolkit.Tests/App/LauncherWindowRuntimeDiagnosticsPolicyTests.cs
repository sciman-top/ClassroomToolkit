using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherWindowRuntimeDiagnosticsPolicyTests
{
    [Fact]
    public void FormatSelectionMessage_ShouldContainReasonTag()
    {
        var message = LauncherWindowRuntimeDiagnosticsPolicy.FormatSelectionMessage(
            LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible);

        message.Should().Contain("[Launcher][Snapshot]");
        message.Should().Contain("selection=fallback-main-bubble-hidden");
    }
}
