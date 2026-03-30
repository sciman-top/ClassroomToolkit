using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerStateChangeSurfaceDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldMapDecisionFlags_ToNoTouchSurfaceDecision()
    {
        var decision = new ImageManagerStateChangeDecision(
            NormalizeOverlayWindowState: true,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true);

        var surfaceDecision = ImageManagerStateChangeSurfaceDecisionPolicy.Resolve(decision);

        surfaceDecision.ShouldTouchSurface.Should().BeFalse();
        surfaceDecision.Surface.Should().Be(ZOrderSurface.None);
        surfaceDecision.RequestZOrderApply.Should().BeTrue();
        surfaceDecision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepFlagsFalse_WhenDecisionDoesNotRequestApply()
    {
        var decision = new ImageManagerStateChangeDecision(
            NormalizeOverlayWindowState: false,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var surfaceDecision = ImageManagerStateChangeSurfaceDecisionPolicy.Resolve(decision);

        surfaceDecision.ShouldTouchSurface.Should().BeFalse();
        surfaceDecision.RequestZOrderApply.Should().BeFalse();
        surfaceDecision.ForceEnforceZOrder.Should().BeFalse();
    }
}
