using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoDocumentRuntimeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoDocumentRuntimeDefaults.PdfDefaultDpi.Should().Be(96);
        PhotoDocumentRuntimeDefaults.PdfCacheLimit.Should().Be(6);
        PhotoDocumentRuntimeDefaults.PdfCacheMaxBytes.Should().Be(100L * 1024L * 1024L);
        PhotoDocumentRuntimeDefaults.PdfCacheTryEnterTimeoutMs.Should().Be(50);
        PhotoDocumentRuntimeDefaults.PdfPrefetchTryEnterTimeoutMs.Should().Be(100);
        PhotoDocumentRuntimeDefaults.PdfPrefetchDelayMs.Should().Be(120);
        PhotoDocumentRuntimeDefaults.NeighborPageCacheLimit.Should().Be(5);
    }
}
