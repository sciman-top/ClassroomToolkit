using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowLifecycleSubscriptionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnCurrentWindowMissing_WhenCurrentIsNull()
    {
        var decision = WindowLifecycleSubscriptionPolicy.Resolve(new object(), null);
        decision.ShouldWire.Should().BeFalse();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.CurrentWindowMissing);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowInstanceChanged_WhenNoPreviousWindow()
    {
        var decision = WindowLifecycleSubscriptionPolicy.Resolve(null, new object());
        decision.ShouldWire.Should().BeTrue();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.WindowInstanceChanged);
    }

    [Fact]
    public void Resolve_ShouldReturnSameWindowInstance_WhenSameWindowInstance()
    {
        var window = new object();
        var decision = WindowLifecycleSubscriptionPolicy.Resolve(window, window);
        decision.ShouldWire.Should().BeFalse();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.SameWindowInstance);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowInstanceChanged_WhenWindowInstanceChanged()
    {
        var decision = WindowLifecycleSubscriptionPolicy.Resolve(new object(), new object());
        decision.ShouldWire.Should().BeTrue();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.WindowInstanceChanged);
    }

    [Fact]
    public void ShouldWire_ShouldMapResolveDecision()
    {
        WindowLifecycleSubscriptionPolicy.ShouldWire(new object(), new object()).Should().BeTrue();
    }
}
