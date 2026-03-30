using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPointerSourceHandlingPolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowContinue_WhenGateAllowsContinue()
    {
        var plan = OverlayPointerSourceHandlingPolicy.Resolve(
            OverlayPointerSourceGateDecision.Continue,
            hideEraserPreviewWhenBlocked: true);

        plan.Should().Be(new OverlayPointerSourceHandlingPlan(
            ShouldContinue: true,
            ShouldMarkHandled: false,
            ShouldHideEraserPreview: false));
    }

    [Fact]
    public void Resolve_ShouldConsumeAndHide_WhenGateConsumesAndHideRequested()
    {
        var plan = OverlayPointerSourceHandlingPolicy.Resolve(
            OverlayPointerSourceGateDecision.Consume,
            hideEraserPreviewWhenBlocked: true);

        plan.Should().Be(new OverlayPointerSourceHandlingPlan(
            ShouldContinue: false,
            ShouldMarkHandled: true,
            ShouldHideEraserPreview: true));
    }

    [Fact]
    public void Resolve_ShouldIgnoreWithoutHandle_WhenGateIgnores()
    {
        var plan = OverlayPointerSourceHandlingPolicy.Resolve(
            OverlayPointerSourceGateDecision.Ignore,
            hideEraserPreviewWhenBlocked: false);

        plan.Should().Be(new OverlayPointerSourceHandlingPlan(
            ShouldContinue: false,
            ShouldMarkHandled: false,
            ShouldHideEraserPreview: false));
    }
}
