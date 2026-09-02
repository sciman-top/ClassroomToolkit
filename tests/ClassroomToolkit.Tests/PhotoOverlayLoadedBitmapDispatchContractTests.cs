using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayLoadedBitmapDispatchContractTests
{
    [Fact]
    public void ApplyLoadedBitmap_ShouldFallbackInline_WhenDispatcherSchedulingFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("new Action(ApplyOverlayLayoutAfterPhotoLoad)");
        source.Should().Contain("if (!scheduled)");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("ApplyOverlayLayoutAfterPhotoLoad();");
    }

    [Fact]
    public void AsyncPhotoLoad_ShouldApplyPendingOriginalScaleCentering_AfterMatchingBitmapArrives()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Loading.cs"));

        var transformIndex = source.IndexOf(
            "ApplyLoadedBitmapTransform(bitmap, useCrossPageUnifiedPath: IsCrossPageDisplayActive());",
            StringComparison.Ordinal);
        var centerIndex = source.IndexOf("ApplyPendingPhotoCenter(bitmap, imagePath);", StringComparison.Ordinal);

        source.Should().Contain("_photoBackgroundSourcePath = imagePath;");
        source.Should().Contain("private void ApplyPendingPhotoCenter(BitmapSource bitmap, string sourcePath)");
        transformIndex.Should().BeGreaterThanOrEqualTo(0);
        centerIndex.Should().BeGreaterThan(transformIndex);
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
