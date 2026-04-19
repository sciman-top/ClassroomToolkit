using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoManipulationAdmissionPolicyTests
{
    [Theory]
    [InlineData(false, false, PaintToolMode.Cursor, false, false, 1, false, false)]
    [InlineData(true, true, PaintToolMode.Cursor, false, false, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Brush, false, false, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, true, false, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, true, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, 1, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, 2, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount,
        bool expectedShouldHandle,
        bool expectedShouldMarkHandled)
    {
        var plan = PhotoManipulationAdmissionPolicy.Resolve(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning,
            activeTouchCount);

        plan.ShouldHandle.Should().Be(expectedShouldHandle);
        plan.ShouldMarkHandled.Should().Be(expectedShouldMarkHandled);
    }
}
