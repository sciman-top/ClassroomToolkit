using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public class WindowCursorHitTestPolicyTests
{
    [Theory]
    [InlineData(10, 10, 0, 0, 100, 100, true)]
    [InlineData(0, 0, 0, 0, 100, 100, true)]
    [InlineData(100, 100, 0, 0, 100, 100, true)]
    [InlineData(-1, 10, 0, 0, 100, 100, false)]
    [InlineData(10, 101, 0, 0, 100, 100, false)]
    public void Resolve_ShouldMatchExpected(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom,
        bool expected)
    {
        var decision = WindowCursorHitTestPolicy.Resolve(cursorX, cursorY, left, top, right, bottom);
        decision.IsInside.Should().Be(expected);
        decision.Reason.Should().Be(expected
            ? WindowCursorHitTestReason.InsideBounds
            : WindowCursorHitTestReason.OutsideBounds);
    }

    [Fact]
    public void IsInside_ShouldMapResolveDecision()
    {
        var actual = WindowCursorHitTestPolicy.IsInside(10, 10, 0, 0, 100, 100);
        actual.Should().BeTrue();
    }
}
