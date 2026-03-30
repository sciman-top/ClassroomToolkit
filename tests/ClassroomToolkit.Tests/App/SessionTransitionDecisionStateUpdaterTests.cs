using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDecisionStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldUpdateBothLastStateFields()
    {
        var runtimeState = FloatingCoordinationRuntimeState.Default;
        var state = new FloatingWindowCoordinationState(
            LastFrontSurface: ZOrderSurface.Whiteboard,
            LastTopmostPlan: new FloatingTopmostPlan(
                ToolbarTopmost: true,
                RollCallTopmost: false,
                LauncherTopmost: true,
                ImageManagerTopmost: false,
                OverlayShouldActivate: true));

        SessionTransitionDecisionStateUpdater.Apply(ref runtimeState, state);

        runtimeState.LastFrontSurface.Should().Be(ZOrderSurface.Whiteboard);
        runtimeState.LastTopmostPlan.Should().NotBeNull();
        runtimeState.LastTopmostPlan!.Value.LauncherTopmost.Should().BeTrue();
    }
}
