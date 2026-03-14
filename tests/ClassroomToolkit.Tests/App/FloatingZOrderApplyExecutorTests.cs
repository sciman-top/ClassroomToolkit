using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingZOrderApplyExecutorTests
{
    [Fact]
    public void Apply_ShouldInvokeRequest_WhenRequested()
    {
        var requested = false;
        var requestedForce = false;

        var applied = FloatingZOrderApplyExecutor.Apply(
            requestZOrderApply: true,
            forceEnforceZOrder: true,
            requestApply: force =>
            {
                requested = true;
                requestedForce = force;
            });

        applied.Should().BeTrue();
        requested.Should().BeTrue();
        requestedForce.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldSkipRequest_WhenNotRequested()
    {
        var requested = false;

        var applied = FloatingZOrderApplyExecutor.Apply(
            requestZOrderApply: false,
            forceEnforceZOrder: true,
            requestApply: _ => requested = true);

        applied.Should().BeFalse();
        requested.Should().BeFalse();
    }

    [Fact]
    public void ApplyTouchResult_ShouldInvokeRequest_WhenTouchChanged_AndApplyPolicyEnabled()
    {
        var requested = false;
        var requestedForce = true;

        var applied = FloatingZOrderApplyExecutor.ApplyTouchResult(
            applyPolicy: true,
            touchChanged: true,
            requestApply: force =>
            {
                requested = true;
                requestedForce = force;
            });

        applied.Should().BeTrue();
        requested.Should().BeTrue();
        requestedForce.Should().BeFalse();
    }

    [Fact]
    public void ApplyTouchResult_ShouldForwardForceFlag_WhenProvided()
    {
        var requested = false;
        var requestedForce = false;

        var applied = FloatingZOrderApplyExecutor.ApplyTouchResult(
            applyPolicy: true,
            touchChanged: true,
            forceEnforceZOrder: true,
            requestApply: force =>
            {
                requested = true;
                requestedForce = force;
            });

        applied.Should().BeTrue();
        requested.Should().BeTrue();
        requestedForce.Should().BeTrue();
    }

    [Fact]
    public void ApplyFloatingRequest_ShouldInvokeRequest_WithForceFlag()
    {
        var requested = false;
        var requestedForce = false;

        var applied = FloatingZOrderApplyExecutor.Apply(
            new FloatingZOrderRequest(ForceEnforceZOrder: true),
            requestApply: force =>
            {
                requested = true;
                requestedForce = force;
            });

        applied.Should().BeTrue();
        requested.Should().BeTrue();
        requestedForce.Should().BeTrue();
    }
}
