using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanMouseExecutionPolicyTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, true)]
    public void ResolveMove_ShouldMatchExpected(
        int routingDecision,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = PhotoPanMouseExecutionPolicy.ResolveMove((PhotoPanMouseMoveRoutingDecision)routingDecision);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }

    [Theory]
    [InlineData(false, false, 0, false)]
    [InlineData(true, false, 0, false)]
    [InlineData(false, true, 0, false)]
    [InlineData(true, true, 2, true)]
    public void ResolveEnd_ShouldMatchExpected(
        bool isMousePhotoPanActive,
        bool shouldEndPan,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = PhotoPanMouseExecutionPolicy.ResolveEnd(
            isMousePhotoPanActive,
            shouldEndPan);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}
