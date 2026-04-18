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
    public void ToolbarNonBoardActions_ShouldClearRegionCaptureResumeArm_AndExitWhiteboardForToolSwitch()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().Contain("PrepareForNonBoardToolbarAction(exitWhiteboard: true);");
        source.Should().Contain("PrepareForNonBoardToolbarAction(exitWhiteboard: false);");
        source.Should().Contain("ClearDirectWhiteboardEntryArm();");
        source.Should().Contain("ExitWhiteboardForToolSwitchIfNeeded();");
        source.Should().Contain("SetBoardActive(false);");
    }

    [Fact]
    public void ToolbarPassthroughCancel_ShouldReplayClickToToolbar()
    {
        var source = File.ReadAllText(GetMainWindowPaintSourcePath());
        var toolbarSource = File.ReadAllText(GetToolbarSourcePath());
        var workflowSource = File.ReadAllText(GetRegionScreenCaptureWorkflowSourcePath());
        var overlaySource = File.ReadAllText(GetRegionSelectionOverlaySourcePath());

        source.Should().Contain("ToolbarPassthroughActivationPolicy.ShouldReplayToolbarClick(");
        source.Should().Contain("_toolbarWindow?.TryActivateButtonAtScreenPoint(");
        toolbarSource.Should().Contain("public bool TryActivateButtonAtScreenPoint(");
        workflowSource.Should().NotContain("SafeShowDialog()");
        workflowSource.Should().Contain("DispatcherFrame");
        overlaySource.Should().NotContain("DialogResult");
        overlaySource.Should().Contain("SelectionAccepted");
    }

    [Fact]
    public void SessionCaptureWhiteboardExit_ShouldRefreshBoardVisualAfterPhotoModeExit()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().Contain("_overlay?.ExitPhotoMode();");
        source.Should().Contain("RefreshBoardButtonVisualState();");
    }

    [Fact]
    public void BoardClick_ShouldClearOtherSelectionVisuals_AndKeepBoardSelectedWhileCaptureIsPending()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().Contain("PreviewMouseDown += OnPreviewMouseDown;");
        source.Should().Contain("ToolbarResumeCancellationPolicy.ShouldCancelPendingResumeOnToolbarPress(");
        source.Should().Contain("ResetToolSelectionBaselineForBoardInteraction();");
        source.Should().Contain("ClearNonBoardSelectionVisualState();");
        source.Should().Contain("_regionCapturePending = true;");
        source.Should().Contain("QuickColor1Button.IsChecked = false;");
        source.Should().Contain("QuickColor2Button.IsChecked = false;");
        source.Should().Contain("QuickColor3Button.IsChecked = false;");
    }

    [Fact]
    public void ToolbarNonBoardPress_ShouldCancelActiveRegionSelection_WhenToolbarIsAboveMask()
    {
        var toolbarSource = File.ReadAllText(GetToolbarSourcePath());
        var workflowSource = File.ReadAllText(GetRegionScreenCaptureWorkflowSourcePath());

        toolbarSource.Should().Contain("RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress()");
        workflowSource.Should().Contain("ToolbarHandledPress");
        workflowSource.Should().Contain("CancelActiveSelectionFromToolbarHandledPress");
    }

    [Fact]
    public void ToolbarHoverDuringPendingCapture_ShouldCancelActiveRegionSelection_AsPointerMove()
    {
        var toolbarSource = File.ReadAllText(GetToolbarSourcePath());
        var workflowSource = File.ReadAllText(GetRegionScreenCaptureWorkflowSourcePath());
        var overlaySource = File.ReadAllText(GetRegionSelectionOverlaySourcePath());

        toolbarSource.Should().Contain("MouseEnter += OnToolbarMouseEnter;");
        toolbarSource.Should().Contain("MouseLeave += OnToolbarMouseLeave;");
        toolbarSource.Should().NotContain("if (!_regionCapturePending)");
        toolbarSource.Should().Contain("RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove()");
        toolbarSource.Should().Contain("TryResumeRegionCaptureIfPointerOutsideToolbar();");
        toolbarSource.Should().Contain("_regionCapturePending");
        workflowSource.Should().Contain("CancelActiveSelectionFromToolbarPointerMove");
        overlaySource.Should().Contain("CancelFromToolbarPointerMove");
        overlaySource.Should().Contain("RegionScreenCapturePassthroughInputKind.PointerMove");
    }

    [Fact]
    public void RegionCaptureSuccess_ShouldRefreshToolbarVisualAfterSessionCaptureEntry()
    {
        var source = File.ReadAllText(GetMainWindowPaintSourcePath());

        source.Should().Contain("ApplyPhotoOverlayEntry(");
        source.Should().Contain("overlay.EnsurePhotoWindowedMode();");
        source.Should().Contain("_toolbarWindow?.SetBoardActive(false);");
    }

    [Fact]
    public void RegionCaptureFlow_ShouldKeepScreenshotVisible_AndAvoidFullscreenEntry()
    {
        var source = File.ReadAllText(GetMainWindowPaintSourcePath());
        var photoSource = File.ReadAllText(GetMainWindowPhotoSourcePath());
        var inputSource = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input*.cs");
        var overlayPhotoNavSource = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation*.cs");
        var overlayPhotoTransformSource = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform*.cs");

        source.Should().Contain("_toolbarWindow?.SetBoardActive(false);");
        source.Should().Contain("allowInkOutsidePhoto: true,");
        source.Should().Contain("preserveImageOriginalScale: true,");
        source.Should().Contain("overlay.EnsurePhotoWindowedMode();");
        source.Should().NotContain("_toolbarWindow?.SetBoardActive(true);");
        photoSource.Should().Contain("overlay.EnterPhotoMode(path);");
        photoSource.Should().Contain("overlay.SetPhotoInkCanvasUnbounded(allowInkOutsidePhoto);");
        photoSource.Should().Contain("overlay.CenterPhotoAtOriginalScale();");
        inputSource.Should().Contain("ZoomPhoto(e.Delta, PhotoZoomAnchorPolicy.ResolveViewportCenter(OverlayRoot));");
        overlayPhotoNavSource.Should().Contain("public void SetPhotoInkCanvasUnbounded(bool enabled)");
        overlayPhotoNavSource.Should().Contain("if (!_photoUnboundedInkCanvasEnabled && PhotoBackground.Source is BitmapSource bitmap)");
        overlayPhotoTransformSource.Should().Contain("public void CenterPhotoAtOriginalScale()");
    }

    [Fact]
    public void RegionCaptureUnboundedInkMode_ShouldBypassOutOfPageMoveSuppression()
    {
        var inputSource = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input*.cs");

        inputSource.Should().Contain("if (_photoUnboundedInkCanvasEnabled)");
        inputSource.Should().Contain("return false;");
    }

    [Fact]
    public void BoardSecondClickDuringPendingCapture_ShouldCancelMaskAndEnterWhiteboardDirectly()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().Contain("if (_regionCapturePending || _directWhiteboardEntryArmed || _resumeRegionCaptureArmed)");
        source.Should().Contain("RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress();");
        source.Should().Contain("if ((_directWhiteboardEntryArmed || _resumeRegionCaptureArmed || _regionCapturePending)");
        source.Should().Contain("ShowBoardHint(\"已进入白板\")");
    }

    [Fact]
    public void BoardInteraction_ShouldResetToolSelectionBaseline_ToAvoidShapeToggleFallback()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().Contain("private void ResetToolSelectionBaselineForBoardInteraction()");
        source.Should().Contain("_toolSelectionManager.Reset(PaintToolMode.Brush);");
        source.Should().Contain("ApplyToolMode(PaintToolMode.Brush);");
        source.Should().Contain("_overlay?.SetMode(PaintToolMode.Brush);");
    }

    [Fact]
    public void RegionCaptureResume_ShouldUseInputPriorityAndMouseLeaveImmediateTrigger()
    {
        var source = File.ReadAllText(GetToolbarSourcePath());

        source.Should().Contain("new DispatcherTimer(DispatcherPriority.Input)");
        source.Should().Contain("Interval = TimeSpan.FromMilliseconds(16)");
        source.Should().Contain("private void OnToolbarMouseLeave");
        source.Should().Contain("TryResumeRegionCaptureIfPointerOutsideToolbar();");
        source.Should().Contain("RegionCaptureResumeTriggerPolicy.Resolve(");
    }

    [Fact]
    public void ToolbarXaml_ShouldMergeRegionCaptureIntoBoardButton()
    {
        var xaml = File.ReadAllText(GetToolbarXamlPath());

        xaml.Should().NotContain("x:Name=\"RegionCaptureButton\"");
        xaml.Should().Contain("ToolTip=\"单击截图，双击白板，长按改色\"");
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

    private static string GetRegionScreenCaptureWorkflowSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "RegionScreenCaptureWorkflow.cs");
    }

    private static string GetRegionSelectionOverlaySourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "RegionSelectionOverlayWindow.xaml.cs");
    }

}
