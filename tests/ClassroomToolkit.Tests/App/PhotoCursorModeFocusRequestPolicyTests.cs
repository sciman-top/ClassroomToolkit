using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoCursorModeFocusRequestPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnFocusRequested_WhenPhotoModeAndCursorMode()
    {
        var decision = PhotoCursorModeFocusRequestPolicy.Resolve(
            photoModeActive: true,
            mode: PaintToolMode.Cursor);

        decision.ShouldRequestFocus.Should().BeTrue();
        decision.Reason.Should().Be(PhotoCursorModeFocusRequestReason.FocusRequested);
    }

    [Theory]
    [InlineData(false, PaintToolMode.Cursor)]
    [InlineData(true, PaintToolMode.Brush)]
    [InlineData(true, PaintToolMode.Eraser)]
    public void Resolve_ShouldReturnFalse_WhenGuardsNotSatisfied(
        bool photoModeActive,
        PaintToolMode mode)
    {
        var decision = PhotoCursorModeFocusRequestPolicy.Resolve(
            photoModeActive: photoModeActive,
            mode: mode);

        decision.ShouldRequestFocus.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestFocus_ShouldMapResolveDecision()
    {
        var shouldRequest = PhotoCursorModeFocusRequestPolicy.ShouldRequestFocus(
            photoModeActive: true,
            mode: PaintToolMode.Cursor);

        shouldRequest.Should().BeTrue();
    }
}
