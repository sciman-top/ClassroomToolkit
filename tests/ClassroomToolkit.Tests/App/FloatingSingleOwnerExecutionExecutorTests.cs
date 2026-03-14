using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingSingleOwnerExecutionExecutorTests
{
    [Fact]
    public void Apply_ShouldForwardActionToExecutor()
    {
        var called = false;

        var result = FloatingSingleOwnerExecutionExecutor.Apply(
            FloatingOwnerBindingAction.AttachOverlay,
            child: "child",
            overlayOwner: "overlay",
            applyAction: (target, owner, action) =>
            {
                called = true;
                target.Should().Be("child");
                owner.Should().Be("overlay");
                action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
                return true;
            });

        result.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldAllowNoOpActions()
    {
        var result = FloatingSingleOwnerExecutionExecutor.Apply(
            FloatingOwnerBindingAction.None,
            child: (string?)null,
            overlayOwner: "overlay",
            applyAction: (_, _, action) => action == FloatingOwnerBindingAction.None);

        result.Should().BeTrue();
    }
}
