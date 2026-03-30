using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDuplicateStateUpdaterTests
{
    [Fact]
    public void Reset_ShouldSetStateToZero()
    {
        long lastId = 12;

        SessionTransitionDuplicateStateUpdater.Reset(ref lastId);

        lastId.Should().Be(0);
    }

    [Fact]
    public void MarkApplied_ShouldAdvanceState_WhenCurrentIdIsGreater()
    {
        long lastId = 5;

        SessionTransitionDuplicateStateUpdater.MarkApplied(ref lastId, currentTransitionId: 8);

        lastId.Should().Be(8);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(8, 7)]
    public void MarkApplied_ShouldKeepState_WhenCurrentIdIsNotGreater(long initialId, long currentId)
    {
        var lastId = initialId;

        SessionTransitionDuplicateStateUpdater.MarkApplied(ref lastId, currentId);

        lastId.Should().Be(initialId);
    }
}
