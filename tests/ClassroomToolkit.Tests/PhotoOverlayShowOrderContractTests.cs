using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayShowOrderContractTests
{
    [Fact]
    public void ShowPhoto_ShouldClearOldPhotoBeforeOverlayVisible_ToAvoidOldFrameFlash()
    {
        var source = File.ReadAllText(GetSourcePath());
        var showPhotoStart = source.IndexOf("public void ShowPhoto(", StringComparison.Ordinal);
        var showPhotoEnd = source.IndexOf("private void OnCanvasSizeChanged(", StringComparison.Ordinal);
        showPhotoStart.Should().BeGreaterThan(0);
        showPhotoEnd.Should().BeGreaterThan(showPhotoStart);

        var showPhotoSource = source.Substring(showPhotoStart, showPhotoEnd - showPhotoStart);

        var clearSourceIndex = showPhotoSource.IndexOf("PhotoImage.Source = null;", StringComparison.Ordinal);
        var ensureVisibleIndex = showPhotoSource.LastIndexOf("EnsureOverlayVisible();", StringComparison.Ordinal);

        clearSourceIndex.Should().BeGreaterThan(0);
        ensureVisibleIndex.Should().BeGreaterThan(0);
        clearSourceIndex.Should().BeLessThan(ensureVisibleIndex);
    }

    [Fact]
    public void ShowPhoto_ShouldKeepWindowTransparentUntilNewBitmapIsApplied()
    {
        var source = File.ReadAllText(GetSourcePath());
        var showPhotoStart = source.IndexOf("public void ShowPhoto(", StringComparison.Ordinal);
        var showPhotoEnd = source.IndexOf("private void OnCanvasSizeChanged(", StringComparison.Ordinal);
        showPhotoStart.Should().BeGreaterThan(0);
        showPhotoEnd.Should().BeGreaterThan(showPhotoStart);

        var showPhotoSource = source.Substring(showPhotoStart, showPhotoEnd - showPhotoStart);
        var transparentGuardIndex = showPhotoSource.IndexOf("Opacity = 0.0;", StringComparison.Ordinal);
        var ensureVisibleIndex = showPhotoSource.LastIndexOf("EnsureOverlayVisible();", StringComparison.Ordinal);

        transparentGuardIndex.Should().BeGreaterThan(0);
        ensureVisibleIndex.Should().BeGreaterThan(0);
        transparentGuardIndex.Should().BeLessThan(ensureVisibleIndex);
        source.Should().Contain("Opacity = 1.0;");
    }

    [Fact]
    public void ShowPhoto_ShouldReapplyTransparentGuard_AfterOverlayBecomesVisible()
    {
        var source = File.ReadAllText(GetSourcePath());
        var showPhotoStart = source.IndexOf("public void ShowPhoto(", StringComparison.Ordinal);
        var showPhotoEnd = source.IndexOf("private void OnCanvasSizeChanged(", StringComparison.Ordinal);
        showPhotoStart.Should().BeGreaterThan(0);
        showPhotoEnd.Should().BeGreaterThan(showPhotoStart);

        var showPhotoSource = source.Substring(showPhotoStart, showPhotoEnd - showPhotoStart);
        var ensureVisibleIndex = showPhotoSource.LastIndexOf("EnsureOverlayVisible();", StringComparison.Ordinal);
        ensureVisibleIndex.Should().BeGreaterThan(0);

        var firstTransparentIndex = showPhotoSource.IndexOf("Opacity = 0.0;", StringComparison.Ordinal);
        firstTransparentIndex.Should().BeGreaterThan(0);

        var transparentAfterVisibleIndex = showPhotoSource.IndexOf("Opacity = 0.0;", ensureVisibleIndex, StringComparison.Ordinal);
        transparentAfterVisibleIndex.Should().BeGreaterThan(ensureVisibleIndex);
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }
}
