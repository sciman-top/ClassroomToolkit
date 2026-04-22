using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerThumbnailCacheWarmupContractTests
{
    [Fact]
    public void QueueThumbnailLoad_ShouldWarmCache_BeforeDiscardingStaleDecodedThumbnail()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (thumbnail != null && !_isClosing)");
        var cacheWarmupIndex = source.IndexOf(
            "PutThumbnailCache(item.Path, isPdf, decodeWidth, item.Modified, thumbnail, pageCount);",
            StringComparison.Ordinal);
        var staleGuardIndex = source.IndexOf(
            "if (thumbnail == null || token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId))",
            StringComparison.Ordinal);

        cacheWarmupIndex.Should().BeGreaterThan(0);
        staleGuardIndex.Should().BeGreaterThan(cacheWarmupIndex);
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
