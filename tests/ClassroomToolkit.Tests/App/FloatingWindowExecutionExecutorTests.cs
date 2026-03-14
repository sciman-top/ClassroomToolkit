using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowExecutionExecutorTests
{
    [Fact]
    public void Apply_ShouldDispatchOwnerActivationAndTopmostPlans()
    {
        var ownerCalled = false;
        var overlayActivated = false;
        var imageManagerActivated = false;
        var topmostCalled = false;
        var plan = new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: true,
                RollCallTopmost: false,
                LauncherTopmost: true,
                ImageManagerTopmost: true,
                EnforceZOrder: true),
            ActivationPlan: new FloatingWindowActivationPlan(
                ActivateOverlay: true,
                ActivateImageManager: true),
            OwnerPlan: new FloatingOwnerExecutionPlan(
                ToolbarAction: FloatingOwnerBindingAction.AttachOverlay,
                RollCallAction: FloatingOwnerBindingAction.None,
                ImageManagerAction: FloatingOwnerBindingAction.AttachOverlay));

        FloatingWindowExecutionExecutor.Apply(
            plan,
            overlayWindow: "overlay",
            toolbarWindow: "toolbar",
            rollCallWindow: "rollcall",
            launcherWindow: "launcher",
            imageManagerWindow: "image",
            applyOwnerPlan: (ownerPlan, overlay, toolbar, rollCall, imageManager) =>
            {
                ownerCalled = true;
                ownerPlan.ToolbarAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
                overlay.Should().Be("overlay");
                toolbar.Should().Be("toolbar");
                rollCall.Should().Be("rollcall");
                imageManager.Should().Be("image");
            },
            tryActivate: (target, shouldActivate) =>
            {
                if (target == "overlay" && shouldActivate)
                {
                    overlayActivated = true;
                }

                if (target == "image" && shouldActivate)
                {
                    imageManagerActivated = true;
                }

                return shouldActivate && target != null;
            },
            applyTopmostPlan: (topmostPlan, toolbar, rollCall, launcher, imageManager) =>
            {
                topmostCalled = true;
                topmostPlan.EnforceZOrder.Should().BeTrue();
                launcher.Should().Be("launcher");
                imageManager.Should().Be("image");
            });

        ownerCalled.Should().BeTrue();
        overlayActivated.Should().BeTrue();
        imageManagerActivated.Should().BeTrue();
        topmostCalled.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldSkipActivation_WhenTargetsAreNull()
    {
        var activationCalls = 0;

        FloatingWindowExecutionExecutor.Apply(
            new FloatingWindowExecutionPlan(
                TopmostExecutionPlan: new FloatingTopmostExecutionPlan(false, false, false, false, false),
                ActivationPlan: new FloatingWindowActivationPlan(true, true),
                OwnerPlan: new FloatingOwnerExecutionPlan(
                    FloatingOwnerBindingAction.None,
                    FloatingOwnerBindingAction.None,
                    FloatingOwnerBindingAction.None)),
            overlayWindow: null,
            toolbarWindow: "toolbar",
            rollCallWindow: null,
            launcherWindow: null,
            imageManagerWindow: null,
            applyOwnerPlan: (_, _, _, _, _) => { },
            tryActivate: (_, _) =>
            {
                activationCalls++;
                return true;
            },
            applyTopmostPlan: (_, _, _, _, _) => { });

        activationCalls.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldContinue_WhenActivationAttemptReturnsFalse()
    {
        var topmostCalled = false;
        var activationCalls = 0;

        FloatingWindowExecutionExecutor.Apply(
            new FloatingWindowExecutionPlan(
                TopmostExecutionPlan: new FloatingTopmostExecutionPlan(false, false, false, false, false),
                ActivationPlan: new FloatingWindowActivationPlan(true, true),
                OwnerPlan: new FloatingOwnerExecutionPlan(
                    FloatingOwnerBindingAction.None,
                    FloatingOwnerBindingAction.None,
                    FloatingOwnerBindingAction.None)),
            overlayWindow: "overlay",
            toolbarWindow: null,
            rollCallWindow: null,
            launcherWindow: null,
            imageManagerWindow: "image",
            applyOwnerPlan: (_, _, _, _, _) => { },
            tryActivate: (_, _) =>
            {
                activationCalls++;
                return false;
            },
            applyTopmostPlan: (_, _, _, _, _) => topmostCalled = true);

        activationCalls.Should().Be(2);
        topmostCalled.Should().BeTrue();
    }
}
