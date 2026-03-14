using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class OverlayLostMouseCaptureExecutionPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool isMousePhotoPanActive,
        bool rightClickPending,
        bool expectedEndPan,
        bool expectedClearPending)
    {
        var plan = OverlayLostMouseCaptureExecutionPolicy.Resolve(
            isMousePhotoPanActive,
            rightClickPending);

        plan.ShouldEndPan.Should().Be(expectedEndPan);
        plan.ShouldClearRightClickPending.Should().Be(expectedClearPending);
    }
}
