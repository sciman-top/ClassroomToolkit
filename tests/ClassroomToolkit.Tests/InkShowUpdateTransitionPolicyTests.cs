using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkShowUpdateTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenStateUnchanged()
    {
        var plan = InkShowUpdateTransitionPolicy.Resolve(
            currentInkShowEnabled: true,
            nextInkShowEnabled: true,
            photoModeActive: true);

        plan.ShouldApplySetting.Should().BeFalse();
        plan.ShouldReturnAfterSetting.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldApplyAndReturn_WhenPhotoModeInactive()
    {
        var plan = InkShowUpdateTransitionPolicy.Resolve(
            currentInkShowEnabled: false,
            nextInkShowEnabled: true,
            photoModeActive: false);

        plan.ShouldApplySetting.Should().BeTrue();
        plan.ShouldReturnAfterSetting.Should().BeTrue();
        plan.ShouldLoadCurrentPage.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldClearState_WhenDisablingInPhotoMode()
    {
        var plan = InkShowUpdateTransitionPolicy.Resolve(
            currentInkShowEnabled: true,
            nextInkShowEnabled: false,
            photoModeActive: true);

        plan.ShouldClearInkState.Should().BeTrue();
        plan.RequestCrossPageUpdateForDisabled.Should().BeTrue();
        plan.ShouldLoadCurrentPage.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldLoadState_WhenEnablingInPhotoMode()
    {
        var plan = InkShowUpdateTransitionPolicy.Resolve(
            currentInkShowEnabled: false,
            nextInkShowEnabled: true,
            photoModeActive: true);

        plan.ShouldLoadCurrentPage.Should().BeTrue();
        plan.RequestCrossPageUpdateForEnabled.Should().BeTrue();
        plan.ShouldClearInkState.Should().BeFalse();
    }
}
