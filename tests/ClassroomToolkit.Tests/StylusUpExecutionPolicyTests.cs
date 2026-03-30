using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StylusUpExecutionPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true, (int)StylusUpExecutionAction.None, false)]
    [InlineData(false, true, true, true, (int)StylusUpExecutionAction.None, false)]
    [InlineData(false, false, false, true, (int)StylusUpExecutionAction.None, false)]
    [InlineData(false, false, true, true, (int)StylusUpExecutionAction.HandleLastStylusPoint, true)]
    [InlineData(false, false, true, false, (int)StylusUpExecutionAction.HandlePointerPosition, true)]
    public void Resolve_ShouldReturnExpectedPlan(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = StylusUpExecutionPolicy.Resolve(
            photoLoading,
            handledByPhotoPan,
            inkOperationActive,
            hasStylusPoints);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}
