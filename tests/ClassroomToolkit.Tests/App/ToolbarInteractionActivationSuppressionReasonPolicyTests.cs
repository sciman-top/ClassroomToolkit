using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionActivationSuppressionReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(
                ToolbarInteractionActivationSuppressionReason.NonActivatedTrigger)
            .Should().Be("non-activated-trigger");
        ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(
                ToolbarInteractionActivationSuppressionReason.PreviewTimestampUnset)
            .Should().Be("preview-timestamp-unset");
        ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(
                ToolbarInteractionActivationSuppressionReason.OutsideSuppressionWindow)
            .Should().Be("outside-window");
        ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(
                ToolbarInteractionActivationSuppressionReason.LauncherOnlyDrift)
            .Should().Be("launcher-only-drift");
        ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(
                ToolbarInteractionActivationSuppressionReason.PreviewAlreadyRetouched)
            .Should().Be("preview-already-retouched");
        ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(
                ToolbarInteractionActivationSuppressionReason.None)
            .Should().Be("not-suppressed");
    }
}
