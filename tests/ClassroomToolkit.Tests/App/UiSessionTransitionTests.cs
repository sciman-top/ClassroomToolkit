using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class UiSessionTransitionTests
{
    [Fact]
    public void HasStateChange_ShouldReturnFalse_WhenPreviousAndCurrentEqual()
    {
        var state = UiSessionState.Default;
        var transition = new UiSessionTransition(
            Id: 1,
            OccurredAtUtc: new System.DateTime(2026, 3, 8, 0, 0, 0, System.DateTimeKind.Utc),
            Event: new MarkInkSavedEvent(),
            Previous: state,
            Current: state);

        transition.HasStateChange.Should().BeFalse();
    }

    [Fact]
    public void HasStateChange_ShouldReturnTrue_WhenPreviousAndCurrentDiffer()
    {
        var previous = UiSessionState.Default;
        var current = previous with { InkDirty = true };
        var transition = new UiSessionTransition(
            Id: 2,
            OccurredAtUtc: new System.DateTime(2026, 3, 8, 0, 0, 1, System.DateTimeKind.Utc),
            Event: new MarkInkDirtyEvent(),
            Previous: previous,
            Current: current);

        transition.HasStateChange.Should().BeTrue();
    }
}
