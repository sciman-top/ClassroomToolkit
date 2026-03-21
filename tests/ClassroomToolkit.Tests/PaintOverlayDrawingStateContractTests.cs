using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayDrawingStateContractTests
{
    [Fact]
    public void PointerCaptureLifecycle_ShouldDriveGlobalDrawingState()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private void CapturePointerInput()");
        source.Should().Contain("PaintModeManager.Instance.IsDrawing = true;");
        source.Should().Contain("private void ReleasePointerInput()");
        source.Should().Contain("PaintModeManager.Instance.IsDrawing = false;");
        source.Should().Contain("private void OnOverlayLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)");
        source.Should().Contain("private void OnOverlayLostStylusCapture(object sender, StylusEventArgs e)");
        source.Should().Contain("private void HandleOverlayCaptureLost()");
        source.Should().Contain("ReleasePointerInput();");
        source.Should().Contain("CancelActivePointerOperationOnCaptureLoss();");
        source.Should().Contain("private void CancelActivePointerOperationOnCaptureLoss()");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.cs");
    }
}
