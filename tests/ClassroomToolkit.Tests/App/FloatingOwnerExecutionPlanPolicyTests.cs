using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingOwnerExecutionPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldMapAttachDetachActions_ForAllFloatingWindows()
    {
        var plan = FloatingOwnerExecutionPlanPolicy.Resolve(
            overlayVisible: true,
            toolbarOwnerAlreadyOverlay: false,
            rollCallOwnerAlreadyOverlay: true,
            imageManagerOwnerAlreadyOverlay: false);

        plan.ToolbarAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        plan.RollCallAction.Should().Be(FloatingOwnerBindingAction.None);
        plan.ImageManagerAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void Resolve_ShouldMapDetachActions_WhenOverlayIsHidden()
    {
        var plan = FloatingOwnerExecutionPlanPolicy.Resolve(
            overlayVisible: false,
            toolbarOwnerAlreadyOverlay: true,
            rollCallOwnerAlreadyOverlay: false,
            imageManagerOwnerAlreadyOverlay: true);

        plan.ToolbarAction.Should().Be(FloatingOwnerBindingAction.DetachOverlay);
        plan.RollCallAction.Should().Be(FloatingOwnerBindingAction.None);
        plan.ImageManagerAction.Should().Be(FloatingOwnerBindingAction.DetachOverlay);
    }

    [Fact]
    public void Resolve_ShouldSupportSnapshotInput()
    {
        var snapshot = FloatingOwnerRuntimeSnapshotPolicy.Resolve(
            overlayVisible: true,
            toolbarOwnerAlreadyOverlay: false,
            rollCallOwnerAlreadyOverlay: true,
            imageManagerOwnerAlreadyOverlay: false);

        var plan = FloatingOwnerExecutionPlanPolicy.Resolve(snapshot);

        plan.ToolbarAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        plan.RollCallAction.Should().Be(FloatingOwnerBindingAction.None);
        plan.ImageManagerAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }
}
