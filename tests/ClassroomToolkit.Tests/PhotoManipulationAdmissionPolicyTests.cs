using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoManipulationAdmissionPolicyTests
{
    [Theory]
    [InlineData(false, false, PaintToolMode.Cursor, false, false, false, false)]
    [InlineData(true, true, PaintToolMode.Cursor, false, false, false, true)]
    [InlineData(true, false, PaintToolMode.Brush, false, false, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, true, false, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, true, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        bool expectedShouldHandle,
        bool expectedShouldMarkHandled)
    {
        var plan = PhotoManipulationAdmissionPolicy.Resolve(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning);

        plan.ShouldHandle.Should().Be(expectedShouldHandle);
        plan.ShouldMarkHandled.Should().Be(expectedShouldMarkHandled);
    }
}
