using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleZOrderApplyGateReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(LauncherBubbleZOrderApplyGateReason.AppClosing)
            .Should().Be("app-closing");
        LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(LauncherBubbleZOrderApplyGateReason.BubbleWindowMissing)
            .Should().Be("bubble-missing");
        LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(LauncherBubbleZOrderApplyGateReason.BubbleHidden)
            .Should().Be("bubble-hidden");
        LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed)
            .Should().Be("visible-changed-suppressed");
        LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(LauncherBubbleZOrderApplyGateReason.CooldownActive)
            .Should().Be("cooldown-active");
        LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(LauncherBubbleZOrderApplyGateReason.None)
            .Should().Be("apply");
    }
}
