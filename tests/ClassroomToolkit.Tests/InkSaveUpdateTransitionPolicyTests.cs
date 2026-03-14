using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkSaveUpdateTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldStopTimer_WhenDisabled()
    {
        var plan = InkSaveUpdateTransitionPolicy.Resolve(enabled: false);

        plan.ShouldStopAutoSaveTimer.Should().BeTrue();
        plan.ShouldCancelPendingAutoSave.Should().BeTrue();
        plan.ShouldScheduleAutoSave.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldScheduleAutoSave_WhenEnabled()
    {
        var plan = InkSaveUpdateTransitionPolicy.Resolve(enabled: true);

        plan.ShouldStopAutoSaveTimer.Should().BeFalse();
        plan.ShouldCancelPendingAutoSave.Should().BeFalse();
        plan.ShouldScheduleAutoSave.Should().BeTrue();
    }
}
