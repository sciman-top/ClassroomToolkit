using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchRuntimeResetReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldMapKnownReasons()
    {
        ToolbarInteractionRetouchRuntimeResetReasonPolicy.ResolveTag(
            ToolbarInteractionRetouchRuntimeResetReason.OverlayClosed).Should().Be("overlay-closed");
        ToolbarInteractionRetouchRuntimeResetReasonPolicy.ResolveTag(
            ToolbarInteractionRetouchRuntimeResetReason.ToolbarClosed).Should().Be("toolbar-closed");
        ToolbarInteractionRetouchRuntimeResetReasonPolicy.ResolveTag(
            ToolbarInteractionRetouchRuntimeResetReason.PaintHidden).Should().Be("paint-hidden");
        ToolbarInteractionRetouchRuntimeResetReasonPolicy.ResolveTag(
            ToolbarInteractionRetouchRuntimeResetReason.RequestExit).Should().Be("request-exit");
    }

    [Fact]
    public void ResolveTag_ShouldFallbackToNone()
    {
        ToolbarInteractionRetouchRuntimeResetReasonPolicy.ResolveTag(
            ToolbarInteractionRetouchRuntimeResetReason.None).Should().Be("none");
    }
}
