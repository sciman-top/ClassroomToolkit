using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarBoardSelectionVisualPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    public void Resolve_ShouldReturnFalse_WhenWhiteboardIsNotActuallyActive(
        bool boardActive,
        bool overlayWhiteboardActive,
        bool sessionCaptureWhiteboardActive,
        bool directWhiteboardEntryArmed)
    {
        var selected = ToolbarBoardSelectionVisualPolicy.Resolve(
            boardActive,
            overlayWhiteboardActive,
            sessionCaptureWhiteboardActive,
            directWhiteboardEntryArmed,
            regionCapturePending: false);

        selected.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Resolve_ShouldReturnTrue_WhenAnyWhiteboardSceneIsActuallyActive(
        bool boardActive,
        bool overlayWhiteboardActive,
        bool sessionCaptureWhiteboardActive)
    {
        var selected = ToolbarBoardSelectionVisualPolicy.Resolve(
            boardActive,
            overlayWhiteboardActive,
            sessionCaptureWhiteboardActive,
            directWhiteboardEntryArmed: false,
            regionCapturePending: false);

        selected.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenDirectWhiteboardEntryIsArmed()
    {
        var selected = ToolbarBoardSelectionVisualPolicy.Resolve(
            boardActive: false,
            overlayWhiteboardActive: false,
            sessionCaptureWhiteboardActive: false,
            directWhiteboardEntryArmed: true,
            regionCapturePending: false);

        selected.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenRegionCaptureIsPending()
    {
        var selected = ToolbarBoardSelectionVisualPolicy.Resolve(
            boardActive: false,
            overlayWhiteboardActive: false,
            sessionCaptureWhiteboardActive: false,
            directWhiteboardEntryArmed: false,
            regionCapturePending: true);

        selected.Should().BeTrue();
    }
}
