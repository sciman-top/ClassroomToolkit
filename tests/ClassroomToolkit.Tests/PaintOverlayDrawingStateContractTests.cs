using AwesomeAssertions;

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

    [Fact]
    public void CloseAndDisplayLifecycle_ShouldReleaseInputAndHandlePerMonitorDpiChanges()
    {
        var lifecycle = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Lifecycle.cs");

        lifecycle.Should().Contain("private const int WmDpiChanged = 0x02E0;");
        lifecycle.Should().Contain("msg == WmDisplayChange || msg == WmDpiChanged");
        lifecycle.Should().Contain("ReleasePointerInput();");
        lifecycle.Should().Contain("EnsureRasterSurface();");

        var program = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Program.cs");
        program.Should().Contain("ApplicationConfiguration.Initialize();");

        var project = File.ReadAllText(
            TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "ClassroomToolkit.App.csproj"));
        project.Should().Contain("<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>");

        var manifest = File.ReadAllText(
            TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "app.manifest"));
        manifest.Should().NotContain("<dpiAware");
        manifest.Should().NotContain("<dpiAwareness");
    }
}
