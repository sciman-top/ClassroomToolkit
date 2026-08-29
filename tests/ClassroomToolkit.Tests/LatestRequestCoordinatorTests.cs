using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LatestRequestCoordinatorTests
{
    [Fact]
    public void Completion_ShouldScheduleOnlyTheNewestRequest_AfterSupersededWorkFinishes()
    {
        var coordinator = new LatestRequestCoordinator<string>();

        coordinator.TryBegin("first", out var first).Should().BeTrue();
        coordinator.TryBegin("second", out _).Should().BeFalse();

        coordinator.IsCurrent(first).Should().BeFalse();
        coordinator.TryComplete(first, out var second).Should().BeTrue();
        second.Request.Should().Be("second");
        coordinator.IsCurrent(second).Should().BeTrue();

        coordinator.TryComplete(second, out _).Should().BeFalse();
    }

    [Fact]
    public void Invalidate_ShouldPreventAnOlderCompletionFromAffectingNewWork()
    {
        var coordinator = new LatestRequestCoordinator<int>();

        coordinator.TryBegin(1, out var first).Should().BeTrue();
        coordinator.Invalidate();
        coordinator.TryBegin(2, out var second).Should().BeTrue();

        coordinator.TryComplete(first, out _).Should().BeFalse();
        coordinator.IsCurrent(second).Should().BeTrue();
        coordinator.TryComplete(second, out _).Should().BeFalse();
    }
}
