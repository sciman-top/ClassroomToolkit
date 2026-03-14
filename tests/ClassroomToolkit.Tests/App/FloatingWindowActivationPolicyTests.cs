using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingWindowActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldActivateOverlay_WhenOverlayNeedsActivation_AndNoUtilityWindowIsActive()
    {
        var plan = FloatingWindowActivationPolicy.Resolve(
            new FloatingWindowActivationSnapshot(
                OverlayVisible: true,
                OverlayShouldActivate: true,
                OverlayActive: false,
                ImageManagerTopmost: false,
                ImageManagerActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: false,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        plan.ActivateOverlay.Should().BeTrue();
        plan.ActivateImageManager.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldActivateImageManager_WhenImageManagerIsFront_AndNoUtilityWindowIsActive()
    {
        var plan = FloatingWindowActivationPolicy.Resolve(
            new FloatingWindowActivationSnapshot(
                OverlayVisible: true,
                OverlayShouldActivate: false,
                OverlayActive: false,
                ImageManagerTopmost: true,
                ImageManagerActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: false,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        plan.ActivateOverlay.Should().BeFalse();
        plan.ActivateImageManager.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldBlockAllActivation_WhenUtilityWindowIsActive()
    {
        var plan = FloatingWindowActivationPolicy.Resolve(
            new FloatingWindowActivationSnapshot(
                OverlayVisible: true,
                OverlayShouldActivate: true,
                OverlayActive: false,
                ImageManagerTopmost: true,
                ImageManagerActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: true,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        plan.ActivateOverlay.Should().BeFalse();
        plan.ActivateImageManager.Should().BeFalse();
    }
}
