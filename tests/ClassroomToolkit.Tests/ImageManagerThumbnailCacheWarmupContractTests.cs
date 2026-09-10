using System.IO;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerThumbnailCacheWarmupContractTests
{
    [Fact]
    public void QueueThumbnailLoad_ShouldWarmCache_BeforeDiscardingStaleDecodedThumbnail()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (thumbnail != null && fingerprint.HasValue && !_isClosing)");
        var cacheWarmupIndex = source.IndexOf(
            "PutThumbnailCache(item.Path, isPdf, decodeWidth, fingerprint.Value, thumbnail, pageCount);",
            StringComparison.Ordinal);
        var staleGuardIndex = source.IndexOf(
            "if (thumbnail == null || token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId))",
            StringComparison.Ordinal);

        cacheWarmupIndex.Should().BeGreaterThan(0);
        staleGuardIndex.Should().BeGreaterThan(cacheWarmupIndex);
    }

    [Fact]
    public void ThumbnailCache_ShouldValidateContentInWorkerPath_AndNotUseScanTimeOnlyMetadata()
    {
        var source = File.ReadAllText(GetSourcePath());
        var schedulingSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.ThumbnailScheduling.cs"));

        source.Should().Contain("PhotoFileFingerprintReader.TryRead(item.Path, out var currentFingerprint)");
        source.Should().NotContain("PutThumbnailCache(item.Path, isPdf, decodeWidth, item.Modified");
        schedulingSource.Should().Contain("PhotoFileFingerprintReader.TryRead(path, out var currentFingerprint)");
        schedulingSource.Should().NotContain("ModifiedTicks");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.Loading.cs");
    }
}
