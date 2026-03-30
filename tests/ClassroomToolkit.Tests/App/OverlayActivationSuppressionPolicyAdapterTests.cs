using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationSuppressionPolicyAdapterTests
{
    [Fact]
    public void ApplySuppression_ShouldDisableOverlayActivation_WhenSuppressed()
    {
        var plan = new FloatingWindowActivationPlan(
            ActivateOverlay: true,
            ActivateImageManager: true);

        var result = OverlayActivationSuppressionPolicyAdapter.ApplySuppression(
            plan,
            suppressOverlayActivation: true);

        result.ActivateOverlay.Should().BeFalse();
        result.ActivateImageManager.Should().BeTrue();
    }

    [Fact]
    public void ApplySuppression_ShouldReturnOriginalPlan_WhenNotSuppressed()
    {
        var plan = new FloatingWindowActivationPlan(
            ActivateOverlay: true,
            ActivateImageManager: false);

        var result = OverlayActivationSuppressionPolicyAdapter.ApplySuppression(
            plan,
            suppressOverlayActivation: false);

        result.Should().Be(plan);
    }
}
