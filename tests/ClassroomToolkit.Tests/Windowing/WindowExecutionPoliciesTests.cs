using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class UserInitiatedWindowActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnActivationRequired_WhenWindowVisible_AndNotActive()
    {
        var decision = WindowExecutionPolicies.ResolveUserInitiatedWindowActivation(
            windowVisible: true,
            windowActive: false);

        decision.ShouldActivateAfterShow.Should().BeTrue();
        decision.Reason.Should().Be(UserInitiatedWindowActivationReason.ActivationRequired);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowNotVisible_WhenWindowNotVisible()
    {
        var decision = WindowExecutionPolicies.ResolveUserInitiatedWindowActivation(
            windowVisible: false,
            windowActive: false);

        decision.ShouldActivateAfterShow.Should().BeFalse();
        decision.Reason.Should().Be(UserInitiatedWindowActivationReason.WindowNotVisible);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowAlreadyActive_WhenWindowAlreadyActive()
    {
        var decision = WindowExecutionPolicies.ResolveUserInitiatedWindowActivation(
            windowVisible: true,
            windowActive: true);

        decision.ShouldActivateAfterShow.Should().BeFalse();
        decision.Reason.Should().Be(UserInitiatedWindowActivationReason.WindowAlreadyActive);
    }

    [Fact]
    public void ShouldActivateAfterShow_ShouldMapResolveDecision()
    {
        var result = WindowExecutionPolicies.ShouldActivateAfterShow(
            windowVisible: true,
            windowActive: false);

        result.Should().BeTrue();
    }
}

public class WindowCursorHitTestPolicyTests
{
    [Theory]
    [InlineData(10, 10, 0, 0, 100, 100, true)]
    [InlineData(0, 0, 0, 0, 100, 100, true)]
    [InlineData(100, 100, 0, 0, 100, 100, true)]
    [InlineData(-1, 10, 0, 0, 100, 100, false)]
    [InlineData(10, 101, 0, 0, 100, 100, false)]
    public void Resolve_ShouldMatchExpected(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom,
        bool expected)
    {
        var decision = WindowExecutionPolicies.ResolveWindowCursorHitTest(cursorX, cursorY, left, top, right, bottom);
        decision.IsInside.Should().Be(expected);
        decision.Reason.Should().Be(expected
            ? WindowCursorHitTestReason.InsideBounds
            : WindowCursorHitTestReason.OutsideBounds);
    }

    [Fact]
    public void IsInside_ShouldMapResolveDecision()
    {
        var actual = WindowExecutionPolicies.IsInside(10, 10, 0, 0, 100, 100);
        actual.Should().BeTrue();
    }
}

public sealed class WindowLifecycleSubscriptionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnCurrentWindowMissing_WhenCurrentIsNull()
    {
        var decision = WindowExecutionPolicies.ResolveWindowLifecycleSubscription(new object(), null);
        decision.ShouldWire.Should().BeFalse();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.CurrentWindowMissing);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowInstanceChanged_WhenNoPreviousWindow()
    {
        var decision = WindowExecutionPolicies.ResolveWindowLifecycleSubscription(null, new object());
        decision.ShouldWire.Should().BeTrue();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.WindowInstanceChanged);
    }

    [Fact]
    public void Resolve_ShouldReturnSameWindowInstance_WhenSameWindowInstance()
    {
        var window = new object();
        var decision = WindowExecutionPolicies.ResolveWindowLifecycleSubscription(window, window);
        decision.ShouldWire.Should().BeFalse();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.SameWindowInstance);
    }

    [Fact]
    public void Resolve_ShouldReturnWindowInstanceChanged_WhenWindowInstanceChanged()
    {
        var decision = WindowExecutionPolicies.ResolveWindowLifecycleSubscription(new object(), new object());
        decision.ShouldWire.Should().BeTrue();
        decision.Reason.Should().Be(WindowLifecycleSubscriptionReason.WindowInstanceChanged);
    }

    [Fact]
    public void ShouldWire_ShouldMapResolveDecision()
    {
        WindowExecutionPolicies.ShouldWire(new object(), new object()).Should().BeTrue();
    }
}
