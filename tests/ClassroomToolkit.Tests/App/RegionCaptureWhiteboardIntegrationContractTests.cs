using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RegionCaptureWhiteboardIntegrationContractTests
{
    [Fact]
    public void ToolbarBoardButton_ShouldUseSingleClickCapture_AndSecondClickEnterWhiteboard()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().NotContain("private readonly DispatcherTimer _boardClickTimer;");
        source.Should().NotContain("private const int BoardClickDecisionDelayMs = 280;");
        source.Should().Contain("if (_boardActive)");
        source.Should().Contain("SetBoardActive(false);");
        source.Should().Contain("if (_overlay?.IsPhotoModeActive == true)");
        source.Should().Contain("SetBoardActive(true);");
        source.Should().Contain("RegionCaptureRequested?.Invoke()");
    }

    [Fact]
    public void RegionCaptureFlow_ShouldKeepScreenshotVisible_AndAvoidFullscreenEntry()
    {
        var source = File.ReadAllText(GetMainWindowPaintSourcePath());
        var photoSource = File.ReadAllText(GetMainWindowPhotoSourcePath());
        var overlayPhotoNavSource = File.ReadAllText(GetOverlayPhotoNavigationSourcePath());
        var overlayPhotoTransformSource = File.ReadAllText(GetOverlayPhotoTransformSourcePath());

        source.Should().Contain("_toolbarWindow?.SetBoardActive(false);");
        source.Should().Contain("allowInkOutsidePhoto: true,");
        source.Should().Contain("preserveImageOriginalScale: true,");
        source.Should().Contain("overlay.EnsurePhotoWindowedMode();");
        source.Should().NotContain("_toolbarWindow?.SetBoardActive(true);");
        photoSource.Should().Contain("overlay.EnterPhotoMode(path);");
        photoSource.Should().Contain("overlay.SetPhotoInkCanvasUnbounded(allowInkOutsidePhoto);");
        photoSource.Should().Contain("overlay.CenterPhotoAtOriginalScale();");
        overlayPhotoNavSource.Should().Contain("public void SetPhotoInkCanvasUnbounded(bool enabled)");
        overlayPhotoNavSource.Should().Contain("if (!_photoUnboundedInkCanvasEnabled && PhotoBackground.Source is BitmapSource bitmap)");
        overlayPhotoTransformSource.Should().Contain("public void CenterPhotoAtOriginalScale()");
    }

    [Fact]
    public void RegionCaptureUnboundedInkMode_ShouldBypassOutOfPageMoveSuppression()
    {
        var inputSource = File.ReadAllText(GetOverlayInputSourcePath());

        inputSource.Should().Contain("if (_photoUnboundedInkCanvasEnabled)");
        inputSource.Should().Contain("return false;");
    }

    [Fact]
    public void ToolbarXaml_ShouldMergeRegionCaptureIntoBoardButton()
    {
        var xaml = File.ReadAllText(GetToolbarXamlPath());

        xaml.Should().NotContain("x:Name=\"RegionCaptureButton\"");
        xaml.Should().Contain("ToolTip=\"单击区域截图，再次点击进白板，长按改颜色\"");
    }

    private static string GetToolbarSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintToolbarWindow.xaml.cs");
    }

    private static string GetMainWindowPaintSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Paint.cs");
    }

    private static string GetToolbarXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintToolbarWindow.xaml");
    }

    private static string GetMainWindowPhotoSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Photo.cs");
    }

    private static string GetOverlayPhotoNavigationSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation.cs");
    }

    private static string GetOverlayInputSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.cs");
    }

    private static string GetOverlayPhotoTransformSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform.cs");
    }
}
