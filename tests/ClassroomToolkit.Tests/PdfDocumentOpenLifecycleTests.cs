using System.Drawing;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfDocumentOpenLifecycleTests
{
    [Fact]
    public void TryOpenPdfDocumentCore_ShouldDisposeAndClearDocument_WhenPdfHasNoPages()
    {
        var fake = new FakePdfDocumentHost(pageCount: 0);

        var opened = PaintOverlayWindow.TryOpenPdfDocumentCore(
            "ignored.pdf",
            _ => fake,
            out var document,
            out var pageCount);

        opened.Should().BeFalse();
        document.Should().BeNull();
        pageCount.Should().Be(0);
        fake.Disposed.Should().BeTrue();
    }

    [Fact]
    public void TryOpenPdfDocumentCore_ShouldTransferOwnership_WhenPdfHasPages()
    {
        var fake = new FakePdfDocumentHost(pageCount: 1);

        var opened = PaintOverlayWindow.TryOpenPdfDocumentCore(
            "ignored.pdf",
            _ => fake,
            out var document,
            out var pageCount);

        opened.Should().BeTrue();
        document.Should().BeSameAs(fake);
        pageCount.Should().Be(1);
        fake.Disposed.Should().BeFalse();

        document!.Dispose();
        fake.Disposed.Should().BeTrue();
    }

    private sealed class FakePdfDocumentHost : IPdfDocumentHost
    {
        public FakePdfDocumentHost(int pageCount)
        {
            PageCount = pageCount;
        }

        public int PageCount { get; }

        public bool Disposed { get; private set; }

        public bool TryGetPageSize(int pageIndex, out SizeF size)
        {
            size = new SizeF(100, 100);
            return !Disposed && pageIndex == 1 && PageCount > 0;
        }

        public BitmapSource? RenderPage(int pageIndex, double dpi)
        {
            return null;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
