using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StylusDownExecutionPolicyTests
{
    [Theory]
    [InlineData(true, false, false, true, (int)StylusDownExecutionAction.None, false, false)]
    [InlineData(false, true, false, true, (int)StylusDownExecutionAction.None, false, false)]
    [InlineData(false, false, true, true, (int)StylusDownExecutionAction.None, false, false)]
    [InlineData(false, false, false, true, (int)StylusDownExecutionAction.HandleFirstStylusPoint, true, true)]
    [InlineData(false, false, false, false, (int)StylusDownExecutionAction.HandlePointerPosition, true, true)]
    public void Resolve_ShouldReturnExpectedPlan(
        bool photoLoading,
        bool handledByPhotoPan,
        bool shouldIgnoreFromPhotoControls,
        bool hasStylusPoints,
        int expectedAction,
        bool expectedReset,
        bool expectedHandled)
    {
        var plan = StylusDownExecutionPolicy.Resolve(
            photoLoading,
            handledByPhotoPan,
            shouldIgnoreFromPhotoControls,
            hasStylusPoints);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldResetTimestampState.Should().Be(expectedReset);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}
