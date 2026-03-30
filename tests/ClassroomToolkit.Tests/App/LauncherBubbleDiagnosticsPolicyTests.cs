using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleDiagnosticsPolicyTests
{
    [Fact]
    public void FormatVisibleChangedGateSkipMessage_ShouldIncludeReasonTag()
    {
        var message = LauncherBubbleDiagnosticsPolicy.FormatVisibleChangedGateSkipMessage(
            LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed);

        message.Should().Be(
            "[LauncherBubble][VisibleChangedGate] skip reason=visible-changed-suppressed");
    }

    [Fact]
    public void FormatVisibleChangedDedupSkipMessage_ShouldIncludeReasonTag()
    {
        var message = LauncherBubbleDiagnosticsPolicy.FormatVisibleChangedDedupSkipMessage(
            LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow);

        message.Should().Be(
            "[LauncherBubble][VisibleChangedDedup] skip reason=duplicate-within-window");
    }

    [Fact]
    public void FormatVisibleChangedGateSkipMessage_ShouldIncludeSourceReasonWhenProvided()
    {
        var message = LauncherBubbleDiagnosticsPolicy.FormatVisibleChangedGateSkipMessage(
            LauncherBubbleZOrderApplyGateReason.CooldownActive,
            LauncherBubbleVisibleChangedApplyReason.CooldownActive);

        message.Should().Be(
            "[LauncherBubble][VisibleChangedGate] skip reason=cooldown-active source=cooldown-active");
    }
}
