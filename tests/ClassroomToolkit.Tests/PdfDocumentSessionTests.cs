using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfDocumentSessionTests
{
    [Fact]
    public void Close_ShouldReleaseDocumentAndClearRenderedPages()
    {
        using var session = new PdfDocumentSession();
        var document = new FakePdfDocumentHost(pageCount: 1);
        session.SetDocument(document, document.PageCount);

        var rendered = session.GetPageBitmap(1, 96);
        rendered.Should().NotBeNull();
        session.GetPageBitmap(1, 96).Should().BeSameAs(rendered);
        document.RenderCount.Should().Be(1);

        session.Close();

        document.Disposed.Should().BeTrue();
        session.HasDocument.Should().BeFalse();
        session.PageCount.Should().Be(0);
        session.GetPageBitmap(1, 96).Should().BeNull();
        session.TryGetCachedPageBitmap(1, out _).Should().BeFalse();
    }

    [Fact]
    public void SetDocument_ShouldReleasePreviousDocumentAndResetPageCache()
    {
        using var session = new PdfDocumentSession();
        var first = new FakePdfDocumentHost(pageCount: 1);
        var second = new FakePdfDocumentHost(pageCount: 2);
        session.SetDocument(first, first.PageCount);
        session.GetPageBitmap(1, 96).Should().NotBeNull();

        session.SetDocument(second, second.PageCount);

        first.Disposed.Should().BeTrue();
        session.PageCount.Should().Be(2);
        session.TryGetCachedPageBitmap(1, out _).Should().BeFalse();
        session.GetPageBitmap(2, 96).Should().NotBeNull();
        second.RenderCount.Should().Be(1);
    }

    [Fact]
    public void TryPrefetchPage_ShouldCheckRequestValidityBeforeRendering()
    {
        using var session = new PdfDocumentSession();
        var document = new FakePdfDocumentHost(pageCount: 1);
        session.SetDocument(document, document.PageCount);

        session.TryPrefetchPage(1, 96, tryEnterTimeoutMs: 0, isRequestCurrent: () => false).Should().BeFalse();
        document.RenderCount.Should().Be(0);

        session.TryPrefetchPage(1, 96, tryEnterTimeoutMs: 0, isRequestCurrent: () => true).Should().BeTrue();
        document.RenderCount.Should().Be(1);
    }

    [Fact]
    public void SetPinnedPages_ShouldKeepVisiblePageInCacheDuringEviction()
    {
        using var session = new PdfDocumentSession();
        var pageCount = PhotoDocumentRuntimeDefaults.PdfCacheLimit + 1;
        var document = new FakePdfDocumentHost(pageCount);
        session.SetDocument(document, document.PageCount);
        session.SetPinnedPages([1]);

        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            session.GetPageBitmap(pageIndex, 96).Should().NotBeNull();
        }

        session.TryGetCachedPageBitmap(1, out _).Should().BeTrue();
        session.TryGetCachedPageBitmap(2, out _).Should().BeFalse();
    }

    private sealed class FakePdfDocumentHost : IPdfDocumentHost
    {
        public FakePdfDocumentHost(int pageCount)
        {
            PageCount = pageCount;
        }

        public int PageCount { get; }

        public int RenderCount { get; private set; }

        public bool Disposed { get; private set; }

        public bool TryGetPageSize(int pageIndex, out SizeF size)
        {
            size = new SizeF(100, 100);
            return !Disposed && pageIndex >= 1 && pageIndex <= PageCount;
        }

        public BitmapSource? RenderPage(int pageIndex, double dpi)
        {
            if (Disposed || pageIndex < 1 || pageIndex > PageCount)
            {
                return null;
            }

            RenderCount++;
            var bitmap = BitmapSource.Create(
                1,
                1,
                dpi,
                dpi,
                PixelFormats.Bgra32,
                palette: null,
                pixels: new byte[4],
                stride: 4);
            bitmap.Freeze();
            return bitmap;
        }

        public void Dispose() => Disposed = true;
    }
}
