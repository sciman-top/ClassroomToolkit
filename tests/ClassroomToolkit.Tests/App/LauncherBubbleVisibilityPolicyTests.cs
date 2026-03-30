using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibilityPolicyTests
{
    [Fact]
    public void Resolve_ShouldRequestForcedZOrder_WhenBubbleVisible()
    {
        var decision = LauncherBubbleVisibilityPolicy.Resolve(bubbleVisible: true);

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDoNothing_WhenBubbleHidden()
    {
        var decision = LauncherBubbleVisibilityPolicy.Resolve(bubbleVisible: false);

        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
