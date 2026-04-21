using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallAuxOverlayTopmostPolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepPhotoOverlayBelowFloatingUtilities()
    {
        var plan = RollCallAuxOverlayTopmostPolicy.Resolve(
            photoOverlayVisible: true,
            groupOverlayVisible: true,
            enforceZOrder: true);

        plan.PhotoOverlayTopmost.Should().BeFalse();
        plan.PhotoOverlayEnforceZOrder.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_ShouldKeepGroupOverlayEnforceBehavior(bool enforceZOrder)
    {
        var plan = RollCallAuxOverlayTopmostPolicy.Resolve(
            photoOverlayVisible: false,
            groupOverlayVisible: true,
            enforceZOrder: enforceZOrder);

        plan.GroupOverlayTopmost.Should().BeTrue();
        plan.GroupOverlayEnforceZOrder.Should().Be(enforceZOrder);
    }
}
