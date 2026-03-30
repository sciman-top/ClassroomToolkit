using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingOwnerExecutionExecutorTests
{
    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenApplyActionIsNull()
    {
        var plan = new FloatingOwnerExecutionPlan(
            ToolbarAction: FloatingOwnerBindingAction.AttachOverlay,
            RollCallAction: FloatingOwnerBindingAction.AttachOverlay,
            ImageManagerAction: FloatingOwnerBindingAction.AttachOverlay);

        var act = () => FloatingOwnerExecutionExecutor.Apply(
            plan,
            new object(),
            new object(),
            new object(),
            new object(),
            applyAction: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldExecutePlan_ForEachFloatingWindow()
    {
        var owner = new OwnerValue();
        var toolbar = new OwnerTarget();
        var rollCall = new OwnerTarget { Owner = owner };
        var imageManager = new OwnerTarget();
        var plan = new FloatingOwnerExecutionPlan(
            ToolbarAction: FloatingOwnerBindingAction.AttachOverlay,
            RollCallAction: FloatingOwnerBindingAction.DetachOverlay,
            ImageManagerAction: FloatingOwnerBindingAction.None);

        FloatingOwnerExecutionExecutor.Apply(
            plan,
            owner,
            toolbar,
            rollCall,
            imageManager,
            (target, currentOwner, action) => WindowOwnerBindingExecutor.TryExecute(
                target,
                currentOwner,
                action,
                attachAction: (window, value) => window.Owner = value,
                detachAction: window => window.Owner = null));

        toolbar.Owner.Should().Be(owner);
        rollCall.Owner.Should().BeNull();
        imageManager.Owner.Should().BeNull();
    }

    [Fact]
    public void Apply_ShouldContinue_WhenOneApplyActionThrowsNonFatal()
    {
        var plan = new FloatingOwnerExecutionPlan(
            ToolbarAction: FloatingOwnerBindingAction.AttachOverlay,
            RollCallAction: FloatingOwnerBindingAction.AttachOverlay,
            ImageManagerAction: FloatingOwnerBindingAction.AttachOverlay);
        var callCount = 0;

        Action act = () => FloatingOwnerExecutionExecutor.Apply(
            plan,
            new object(),
            new object(),
            new object(),
            new object(),
            (target, owner, action) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("apply-failed");
                }

                return true;
            });

        act.Should().NotThrow();
        callCount.Should().Be(3);
    }

    private sealed class OwnerTarget
    {
        public OwnerValue? Owner { get; set; }
    }

    private sealed class OwnerValue
    {
    }
}
