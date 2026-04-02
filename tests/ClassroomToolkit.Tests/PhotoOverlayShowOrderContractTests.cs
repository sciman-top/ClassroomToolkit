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

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }
}
