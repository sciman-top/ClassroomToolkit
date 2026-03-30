using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageUpdateReplayPolicyTests
{
    [Theory]
    [InlineData("Interaction", true)]
    [InlineData("VisualSync", true)]
    [InlineData("BackgroundRefresh", false)]
    public void ShouldQueueReplay_ShouldMatchSourceKind(
        string kindName,
        bool expected)
    {
        var kind = Enum.Parse<CrossPageUpdateSourceKind>(kindName);
        CrossPageUpdateReplayPolicy.ShouldQueueReplay(kind).Should().Be(expected);
    }

    [Fact]
    public void ShouldFlushReplay_ShouldReturnTrue_OnlyWhenRuntimeIsFlushable()
    {
        CrossPageUpdateReplayPolicy.ShouldFlushReplay(
                replayPending: true,
                crossPageUpdatePending: false,
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                interactionActive: false)
            .Should()
            .BeTrue();

        CrossPageUpdateReplayPolicy.ShouldFlushReplay(
                replayPending: false,
                crossPageUpdatePending: false,
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                interactionActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldFlushReplay_ShouldReturnFalse_WhenInteractionIsActive()
    {
        CrossPageUpdateReplayPolicy.ShouldFlushReplay(
                replayPending: true,
                crossPageUpdatePending: false,
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                interactionActive: true)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.InkVisualSyncReplay, true)]
    [InlineData(CrossPageUpdateSources.InteractionReplay, true)]
    [InlineData(CrossPageUpdateSources.PhotoPan, false)]
    public void IsReplayBaseSource_ShouldMatchExpected(string source, bool expected)
    {
        CrossPageUpdateReplayPolicy.IsReplayBaseSource(source).Should().Be(expected);
    }
}
