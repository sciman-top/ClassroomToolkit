using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchDecisionReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        ToolbarInteractionRetouchDecisionReasonPolicy.ResolveTag(ToolbarInteractionRetouchDecisionReason.PreviewMouseDown)
            .Should().Be("preview-mousedown");
        ToolbarInteractionRetouchDecisionReasonPolicy.ResolveTag(ToolbarInteractionRetouchDecisionReason.SceneNotInteractive)
            .Should().Be("scene-not-interactive");
        ToolbarInteractionRetouchDecisionReasonPolicy.ResolveTag(ToolbarInteractionRetouchDecisionReason.NoTopmostDrift)
            .Should().Be("no-topmost-drift");
        ToolbarInteractionRetouchDecisionReasonPolicy.ResolveTag(ToolbarInteractionRetouchDecisionReason.None)
            .Should().Be("retouch");
    }
}
