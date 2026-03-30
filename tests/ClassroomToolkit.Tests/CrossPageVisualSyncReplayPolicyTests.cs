using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageVisualSyncReplayPolicyTests
{
    [Theory]
    [InlineData("ink-state-changed", true)]
    [InlineData("ink-state-changed-delayed", true)]
    [InlineData("ink-redraw-completed", true)]
    [InlineData("ink-redraw-completed-immediate", true)]
    [InlineData("region-erase-crosspage", true)]
    [InlineData("region-erase-crosspage-delayed", true)]
    [InlineData("undo-snapshot", true)]
    [InlineData("ink-show-disabled", true)]
    [InlineData("photo-pan", false)]
    public void IsVisualSyncSource_ShouldMatchExpected(string source, bool expected)
    {
        CrossPageVisualSyncReplayPolicy.IsVisualSyncSource(source).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "ink-state-changed", true)]
    [InlineData(true, "ink-redraw-completed", true)]
    [InlineData(true, "region-erase-crosspage", true)]
    [InlineData(true, "photo-pan", false)]
    [InlineData(false, "ink-state-changed", false)]
    public void ShouldQueueReplay_ShouldMatchExpected(bool pending, string source, bool expected)
    {
        CrossPageVisualSyncReplayPolicy.ShouldQueueReplay(pending, source).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, true, true, false, true)]
    [InlineData(false, false, true, true, false, false)]
    [InlineData(true, true, true, true, false, false)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(true, false, true, false, false, false)]
    [InlineData(true, false, true, true, true, false)]
    public void ShouldFlushReplay_ShouldMatchExpected(
        bool replayPending,
        bool crossPageUpdatePending,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive,
        bool expected)
    {
        CrossPageVisualSyncReplayPolicy
            .ShouldFlushReplay(
                replayPending,
                crossPageUpdatePending,
                photoModeActive,
                crossPageDisplayEnabled,
                interactionActive)
            .Should()
            .Be(expected);
    }
}
