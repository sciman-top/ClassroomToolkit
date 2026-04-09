using System;
using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayAutoCloseTriggerContractTests
{
    [Fact]
    public void ApplyLoadedBitmap_ShouldStartAutoCloseTimer_AfterBitmapIsApplied()
    {
        var source = File.ReadAllText(GetSourcePath());
        var applyStart = source.IndexOf("private void ApplyLoadedBitmap(", StringComparison.Ordinal);
        var loadBitmapStart = source.IndexOf("private static Task<BitmapImage?> LoadBitmapAsync(", StringComparison.Ordinal);
        applyStart.Should().BeGreaterThan(0);
        loadBitmapStart.Should().BeGreaterThan(applyStart);

        var applySource = source.Substring(applyStart, loadBitmapStart - applyStart);
        applySource.Should().Contain("UpdateAutoCloseTimer(durationSeconds);");
        applySource.Should().Contain("PhotoImage.Source = bitmap;");
    }

    [Fact]
    public void ShowPhoto_ShouldNotUseDeadlineBasedAutoCloseInAdvance()
    {
        var source = File.ReadAllText(GetSourcePath());
        var showStart = source.IndexOf("public void ShowPhoto(", StringComparison.Ordinal);
        var showEnd = source.IndexOf("private void OnCanvasSizeChanged(", StringComparison.Ordinal);
        showStart.Should().BeGreaterThan(0);
        showEnd.Should().BeGreaterThan(showStart);

        var showSource = source.Substring(showStart, showEnd - showStart);
        showSource.Should().NotContain("UpdateAutoCloseDeadline(");
        showSource.Should().NotContain("ApplyAutoCloseTimerFromDeadline(");
    }

    [Fact]
    public void ShowPhoto_ShouldStopPreviousAutoCloseTimer_BeforeLoadingNewBitmap()
    {
        var source = File.ReadAllText(GetSourcePath());
        var showStart = source.IndexOf("public void ShowPhoto(", StringComparison.Ordinal);
        var showEnd = source.IndexOf("private void OnCanvasSizeChanged(", StringComparison.Ordinal);
        showStart.Should().BeGreaterThan(0);
        showEnd.Should().BeGreaterThan(showStart);

        var showSource = source.Substring(showStart, showEnd - showStart);
        var requestIdIndex = showSource.IndexOf("var requestId = Interlocked.Increment(ref _photoLoadRequestId);", StringComparison.Ordinal);
        var stopTimerIndex = showSource.IndexOf("_autoCloseTimer.Stop();", StringComparison.Ordinal);
        var loadBitmapIndex = showSource.IndexOf("var bitmap = await LoadBitmapAsync(path);", StringComparison.Ordinal);

        requestIdIndex.Should().BeGreaterThan(0);
        stopTimerIndex.Should().BeGreaterThan(requestIdIndex);
        loadBitmapIndex.Should().BeGreaterThan(stopTimerIndex);
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
