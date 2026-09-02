using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayTopmostNoActivateContractTests
{
    [Fact]
    public void Constructor_ShouldDisableActivation_WhenShowingPhotoOverlay()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("ShowActivated = false;");
        source.Should().NotContain("ShowActivated = true;");
    }

    [Fact]
    public void EnsureOverlayVisible_ShouldEnterTopmostBandBehindCriticalAnchor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private Window? _zOrderAnchor;");
        source.Should().Contain("WindowTopmostExecutor.PrepareNoActivateBehind(this, _zOrderAnchor);");
        source.Should().Contain("WindowTopmostExecutor.ApplyNoActivateBehind(this, _zOrderAnchor);");
        source.Should().NotContain("WindowTopmostExecutor.ApplyNoActivate(this, enabled: true, enforceZOrder: false);");
    }

    [Fact]
    public void EnsureOverlayVisible_ShouldPrepareHiddenWindowBehindAnchorBeforeShow()
    {
        var source = File.ReadAllText(GetSourcePath());
        var methodStart = source.IndexOf("private bool EnsureOverlayVisible()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void UpdateStudentName(", StringComparison.Ordinal);
        methodStart.Should().BeGreaterThan(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var methodSource = source.Substring(methodStart, methodEnd - methodStart);
        var prepareIndex = methodSource.IndexOf(
            "WindowTopmostExecutor.PrepareNoActivateBehind(this, _zOrderAnchor);",
            StringComparison.Ordinal);
        var showIndex = methodSource.IndexOf("Show();", StringComparison.Ordinal);
        var applyIndex = methodSource.IndexOf(
            "WindowTopmostExecutor.ApplyNoActivateBehind(this, _zOrderAnchor);",
            StringComparison.Ordinal);

        prepareIndex.Should().BeGreaterThanOrEqualTo(0);
        showIndex.Should().BeGreaterThan(prepareIndex);
        applyIndex.Should().BeGreaterThan(showIndex);
    }

    [Fact]
    public void RollCallAuxOverlayRetouch_ShouldKeepPhotoBehindCriticalAnchor()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Windowing.cs"));

        source.Should().Contain("WindowTopmostExecutor.ApplyNoActivateBehind(_photoOverlay, photoZOrderAnchor);");
    }

    [Fact]
    public void RollCallPhotoFlow_ShouldPassMainWindowZOrderAnchorToPhotoOverlay()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Photo.cs"));

        source.Should().Contain("var zOrderAnchor = ResolvePhotoOverlayZOrderAnchor();");
        source.Should().Contain("mainWindow.ResolvePhotoOverlayZOrderAnchor()");
    }

    [Fact]
    public void EnsureOverlayVisible_ShouldRequestImmediateMainWindowRetouch_WhenOverlayBecomesVisible()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var becameVisible = false;");
        source.Should().Contain("mainWindow.RequestImmediateFloatingZOrderRetouch();");
    }

    [Fact]
    public void ApplyLoadedBitmap_ShouldDeferReveal_WhenHiddenWindowBecomesVisible()
    {
        var source = File.ReadAllText(GetSourcePath());
        var methodStart = source.IndexOf("private void ApplyLoadedBitmap(", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("void ApplyOverlayLayoutAfterPhotoLoad()", methodStart, StringComparison.Ordinal);
        methodStart.Should().BeGreaterThan(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var methodSource = source.Substring(methodStart, methodEnd - methodStart);
        var becameVisibleIndex = methodSource.IndexOf("becameVisible = EnsureOverlayVisible();", StringComparison.Ordinal);
        var deferredRevealIndex = methodSource.IndexOf("DeferRevealAfterInitialZOrderRetouch(requestId);", StringComparison.Ordinal);
        var immediateRevealGuardIndex = methodSource.IndexOf("if (!becameVisible)", StringComparison.Ordinal);
        var immediateRevealIndex = methodSource.IndexOf("RevealOverlay();", immediateRevealGuardIndex, StringComparison.Ordinal);

        becameVisibleIndex.Should().BeGreaterThanOrEqualTo(0);
        deferredRevealIndex.Should().BeGreaterThan(becameVisibleIndex);
        immediateRevealGuardIndex.Should().BeGreaterThan(deferredRevealIndex);
        immediateRevealIndex.Should().BeGreaterThan(immediateRevealGuardIndex);
    }

    [Fact]
    public void DeferredReveal_ShouldRetouchBehindAnchorBeforeOpacityIsRestored()
    {
        var source = File.ReadAllText(GetSourcePath());
        var methodStart = source.IndexOf("private void DeferRevealAfterInitialZOrderRetouch(", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void RevealOverlay()", methodStart, StringComparison.Ordinal);
        methodStart.Should().BeGreaterThan(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var methodSource = source.Substring(methodStart, methodEnd - methodStart);
        var applyBehindIndex = methodSource.IndexOf(
            "WindowTopmostExecutor.ApplyNoActivateBehind(this, _zOrderAnchor);",
            StringComparison.Ordinal);
        var mainRetouchIndex = methodSource.IndexOf(
            "mainWindow.RequestImmediateFloatingZOrderRetouch();",
            StringComparison.Ordinal);
        var revealIndex = methodSource.IndexOf("RevealOverlay();", StringComparison.Ordinal);

        applyBehindIndex.Should().BeGreaterThanOrEqualTo(0);
        mainRetouchIndex.Should().BeGreaterThan(applyBehindIndex);
        revealIndex.Should().BeGreaterThan(mainRetouchIndex);
    }

    [Fact]
    public void RevealOverlay_ShouldRestoreHitTestingAndOpacityTogether()
    {
        var source = File.ReadAllText(GetSourcePath());
        var methodStart = source.IndexOf("private void RevealOverlay()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void UpdateStudentName(", methodStart, StringComparison.Ordinal);
        methodStart.Should().BeGreaterThan(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var methodSource = source.Substring(methodStart, methodEnd - methodStart);

        methodSource.Should().Contain("IsHitTestVisible = true;");
        methodSource.Should().Contain("Opacity = 1.0;");
    }

    [Fact]
    public void Xaml_ShouldAvoidTopmostBeforeRuntimeAnchorIsKnown()
    {
        var xaml = File.ReadAllText(GetXamlPath());

        xaml.Should().Contain("Topmost=\"False\"");
        xaml.Should().NotContain("Topmost=\"True\"");
    }

    [Fact]
    public void Xaml_ShouldFitPhotoUniformly_AndKeepAspectRatioWithoutForcedCrop()
    {
        var xaml = File.ReadAllText(GetXamlPath());

        xaml.Should().Contain("Stretch=\"Uniform\"");
        xaml.Should().Contain("StretchDirection=\"Both\"");
        xaml.Should().NotContain("Stretch=\"UniformToFill\"");
    }

    [Fact]
    public void ApplyWindowedBounds_ShouldSyncLogicalLayoutBeforeNativePlacement()
    {
        var source = File.ReadAllText(GetSourcePath());
        var methodStart = source.IndexOf("private void ApplyWindowedBounds()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private IntPtr ResolveOverlayWindowHandle()", methodStart, StringComparison.Ordinal);
        methodStart.Should().BeGreaterThan(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var methodSource = source.Substring(methodStart, methodEnd - methodStart);
        var dipBoundsIndex = methodSource.IndexOf("var dipBounds = ResolveScreenBoundsInDip(screenBounds);", StringComparison.Ordinal);
        var widthIndex = methodSource.IndexOf("Width = dipBounds.Width;", StringComparison.Ordinal);
        var nativePlacementIndex = methodSource.IndexOf("WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(", StringComparison.Ordinal);

        dipBoundsIndex.Should().BeGreaterThanOrEqualTo(0);
        widthIndex.Should().BeGreaterThan(dipBoundsIndex);
        nativePlacementIndex.Should().BeGreaterThan(widthIndex);
    }

    [Fact]
    public void MainWindowZOrder_ShouldRetouchPhotoOverlayBeforeCriticalFloatingUtilities()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.ZOrder.cs"));

        var photoRetouchIndex = source.IndexOf(
            "_rollCallWindow?.RetouchAuxOverlayWindowsTopmost(strictEnforceZOrder, ResolvePhotoOverlayZOrderAnchor());",
            StringComparison.Ordinal);
        var toolbarRetouchIndex = source.IndexOf(
            "WindowTopmostExecutor.ApplyNoActivate(_toolbarWindow, toolbarVisible, strictEnforceZOrder);",
            StringComparison.Ordinal);

        photoRetouchIndex.Should().BeGreaterThanOrEqualTo(0);
        toolbarRetouchIndex.Should().BeGreaterThanOrEqualTo(0);
        photoRetouchIndex.Should().BeLessThan(toolbarRetouchIndex);
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }

    private static string GetXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml");
    }
}
