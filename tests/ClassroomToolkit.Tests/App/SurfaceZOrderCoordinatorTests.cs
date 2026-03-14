using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class SurfaceZOrderCoordinatorTests
{
    [Fact]
    public void Apply_ShouldTouchAndRequestApply_WhenDecisionRequiresBoth()
    {
        var touchedSurface = default(ZOrderSurface);
        var touched = false;
        var requested = false;
        var forced = false;

        SurfaceZOrderCoordinator.Apply(
            new SurfaceZOrderDecision(
                ShouldTouchSurface: true,
                Surface: ZOrderSurface.PhotoFullscreen,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true),
            touchSurface: surface =>
            {
                touched = true;
                touchedSurface = surface;
                return true;
            },
            requestApply: force =>
            {
                requested = true;
                forced = force;
            });

        touched.Should().BeTrue();
        touchedSurface.Should().Be(ZOrderSurface.PhotoFullscreen);
        requested.Should().BeTrue();
        forced.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldNotRequest_WhenTouchDidNotChangeAndDecisionDoesNotRequest()
    {
        var requested = false;

        SurfaceZOrderCoordinator.Apply(
            new SurfaceZOrderDecision(
                ShouldTouchSurface: true,
                Surface: ZOrderSurface.Whiteboard,
                RequestZOrderApply: false,
                ForceEnforceZOrder: true),
            touchSurface: _ => false,
            requestApply: _ => requested = true);

        requested.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldRequestWithoutTouch_WhenDecisionOnlyRequestsApply()
    {
        var requested = false;
        var forced = false;

        SurfaceZOrderCoordinator.Apply(
            new SurfaceZOrderDecision(
                ShouldTouchSurface: false,
                Surface: ZOrderSurface.None,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false),
            touchSurface: _ => throw new Xunit.Sdk.XunitException("touch should not be called"),
            requestApply: force =>
            {
                requested = true;
                forced = force;
            });

        requested.Should().BeTrue();
        forced.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldSkipRequest_WhenTouchDoesNotChange_AndForceIsDisabled()
    {
        var requested = false;

        SurfaceZOrderCoordinator.Apply(
            new SurfaceZOrderDecision(
                ShouldTouchSurface: true,
                Surface: ZOrderSurface.PhotoFullscreen,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false),
            touchSurface: _ => false,
            requestApply: _ => requested = true);

        requested.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldRequest_WhenTouchDoesNotChange_ButForceIsEnabled()
    {
        var requested = false;
        var forced = false;

        SurfaceZOrderCoordinator.Apply(
            new SurfaceZOrderDecision(
                ShouldTouchSurface: true,
                Surface: ZOrderSurface.PhotoFullscreen,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true),
            touchSurface: _ => false,
            requestApply: force =>
            {
                requested = true;
                forced = force;
            });

        requested.Should().BeTrue();
        forced.Should().BeTrue();
    }
}
