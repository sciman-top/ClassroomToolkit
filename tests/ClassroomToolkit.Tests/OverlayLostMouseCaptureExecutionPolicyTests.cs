using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class OverlayLostMouseCaptureExecutionPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(false, true, false, false, true, false)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(true, true, true, true, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool isMousePhotoPanActive,
        bool rightClickPending,
        bool inkOperationActive,
        bool expectedEndPan,
        bool expectedClearPending,
        bool expectedCancelInkOperation)
    {
        var plan = OverlayLostMouseCaptureExecutionPolicy.Resolve(
            isMousePhotoPanActive,
            rightClickPending,
            inkOperationActive);

        plan.ShouldEndPan.Should().Be(expectedEndPan);
        plan.ShouldClearRightClickPending.Should().Be(expectedClearPending);
        plan.ShouldCancelInkOperation.Should().Be(expectedCancelInkOperation);
    }
}
