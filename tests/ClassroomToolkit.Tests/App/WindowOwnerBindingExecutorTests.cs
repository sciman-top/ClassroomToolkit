using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowOwnerBindingExecutorTests
{
    [Fact]
    public void TryExecute_ShouldAttachOwner_WhenActionIsAttachOverlay()
    {
        var child = new OwnerTarget();
        var owner = new OwnerValue();

        var result = WindowOwnerBindingExecutor.TryExecute(
            child,
            owner,
            FloatingOwnerBindingAction.AttachOverlay,
            attachAction: (target, value) => target.Owner = value,
            detachAction: target => target.Owner = null);

        result.Should().BeTrue();
        child.Owner.Should().Be(owner);
    }

    [Fact]
    public void TryExecute_ShouldDetachOwner_WhenActionIsDetachOverlay()
    {
        var owner = new OwnerValue();
        var child = new OwnerTarget { Owner = owner };

        var result = WindowOwnerBindingExecutor.TryExecute(
            child,
            owner,
            FloatingOwnerBindingAction.DetachOverlay,
            attachAction: (target, value) => target.Owner = value,
            detachAction: target => target.Owner = null);

        result.Should().BeTrue();
        child.Owner.Should().BeNull();
    }

    [Fact]
    public void TryExecute_ShouldReturnFalse_WhenActionIsNone()
    {
        var child = new OwnerTarget();
        var owner = new OwnerValue();

        var result = WindowOwnerBindingExecutor.TryExecute(
            child,
            owner,
            FloatingOwnerBindingAction.None,
            attachAction: (target, value) => target.Owner = value,
            detachAction: target => target.Owner = null);

        result.Should().BeFalse();
        child.Owner.Should().BeNull();
    }

    private sealed class OwnerTarget
    {
        public OwnerValue? Owner { get; set; }
    }

    private sealed class OwnerValue
    {
    }
}
