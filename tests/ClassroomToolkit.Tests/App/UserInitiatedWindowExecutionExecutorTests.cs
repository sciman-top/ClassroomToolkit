using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class UserInitiatedWindowExecutionExecutorTests
{
    [Fact]
    public void Apply_WithDecision_ShouldForwardActivationRequest()
    {
        var called = false;
        var decision = new UserInitiatedWindowActivationDecision(
            ShouldActivateAfterShow: true,
            Reason: UserInitiatedWindowActivationReason.ActivationRequired);

        var result = UserInitiatedWindowExecutionExecutor.Apply(
            window: "window",
            decision,
            tryActivate: (target, activate) =>
            {
                called = true;
                target.Should().Be("window");
                activate.Should().BeTrue();
                return true;
            });

        result.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldForwardActivationRequest()
    {
        var called = false;

        var result = UserInitiatedWindowExecutionExecutor.Apply(
            window: "window",
            shouldActivate: true,
            tryActivate: (target, activate) =>
            {
                called = true;
                target.Should().Be("window");
                activate.Should().BeTrue();
                return true;
            });

        result.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldAllowFalseResult()
    {
        var result = UserInitiatedWindowExecutionExecutor.Apply(
            window: "window",
            shouldActivate: false,
            tryActivate: (_, activate) => activate);

        result.Should().BeFalse();
    }
}
