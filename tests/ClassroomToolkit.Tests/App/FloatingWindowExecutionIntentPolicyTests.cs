using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingWindowExecutionIntentPolicyTests
{
    [Fact]
    public void ResolveActivationIntent_ShouldReturnExpectedReason()
    {
        var none = FloatingWindowExecutionIntentPolicy.ResolveActivationIntent(
            new FloatingWindowActivationPlan(ActivateOverlay: false, ActivateImageManager: false));
        var overlay = FloatingWindowExecutionIntentPolicy.ResolveActivationIntent(
            new FloatingWindowActivationPlan(ActivateOverlay: true, ActivateImageManager: false));
        var imageManager = FloatingWindowExecutionIntentPolicy.ResolveActivationIntent(
            new FloatingWindowActivationPlan(ActivateOverlay: false, ActivateImageManager: true));

        none.HasIntent.Should().BeFalse();
        none.Reason.Should().Be(FloatingActivationIntentReason.None);
        overlay.HasIntent.Should().BeTrue();
        overlay.Reason.Should().Be(FloatingActivationIntentReason.OverlayActivationRequested);
        imageManager.HasIntent.Should().BeTrue();
        imageManager.Reason.Should().Be(FloatingActivationIntentReason.ImageManagerActivationRequested);
    }

    [Fact]
    public void ResolveOwnerBindingIntent_ShouldReturnExpectedReason()
    {
        var none = FloatingWindowExecutionIntentPolicy.ResolveOwnerBindingIntent(new FloatingOwnerExecutionPlan(
            FloatingOwnerBindingAction.None,
            FloatingOwnerBindingAction.None,
            FloatingOwnerBindingAction.None));
        var toolbar = FloatingWindowExecutionIntentPolicy.ResolveOwnerBindingIntent(new FloatingOwnerExecutionPlan(
            FloatingOwnerBindingAction.AttachOverlay,
            FloatingOwnerBindingAction.None,
            FloatingOwnerBindingAction.None));
        var rollCall = FloatingWindowExecutionIntentPolicy.ResolveOwnerBindingIntent(new FloatingOwnerExecutionPlan(
            FloatingOwnerBindingAction.None,
            FloatingOwnerBindingAction.AttachOverlay,
            FloatingOwnerBindingAction.None));
        var imageManager = FloatingWindowExecutionIntentPolicy.ResolveOwnerBindingIntent(new FloatingOwnerExecutionPlan(
            FloatingOwnerBindingAction.None,
            FloatingOwnerBindingAction.None,
            FloatingOwnerBindingAction.AttachOverlay));

        none.HasIntent.Should().BeFalse();
        none.Reason.Should().Be(FloatingOwnerBindingIntentReason.None);
        toolbar.HasIntent.Should().BeTrue();
        toolbar.Reason.Should().Be(FloatingOwnerBindingIntentReason.ToolbarOwnerBindingRequested);
        rollCall.HasIntent.Should().BeTrue();
        rollCall.Reason.Should().Be(FloatingOwnerBindingIntentReason.RollCallOwnerBindingRequested);
        imageManager.HasIntent.Should().BeTrue();
        imageManager.Reason.Should().Be(FloatingOwnerBindingIntentReason.ImageManagerOwnerBindingRequested);
    }

    [Fact]
    public void BooleanWrappers_ShouldMapResolveDecision()
    {
        FloatingWindowExecutionIntentPolicy.HasActivationIntent(
            new FloatingWindowActivationPlan(ActivateOverlay: false, ActivateImageManager: false)).Should().BeFalse();
        FloatingWindowExecutionIntentPolicy.HasOwnerBindingIntent(
            new FloatingOwnerExecutionPlan(
                FloatingOwnerBindingAction.AttachOverlay,
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None)).Should().BeTrue();
    }
}
