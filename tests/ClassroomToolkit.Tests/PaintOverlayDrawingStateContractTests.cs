using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayDrawingStateContractTests
{
    [Fact]
    public void PointerCaptureLifecycle_ShouldDriveGlobalDrawingState()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input*.cs");

        source.Should().Contain("private void CapturePointerInput()");
        source.Should().Contain("PaintModeManager.Instance.IsDrawing = true;");
        source.Should().Contain("private void ReleasePointerInput()");
        source.Should().Contain("PaintModeManager.Instance.IsDrawing = false;");
        source.Should().Contain("private void OnOverlayLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)");
    }
}
