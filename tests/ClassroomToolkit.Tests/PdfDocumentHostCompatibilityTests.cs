using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfDocumentHostCompatibilityTests
{
    [Fact]
    public void OpenAndRender_ShouldPreserveLetterPageGeometryAtRequestedDpi()
    {
        var path = WritePdf(MinimalPdfFixtureBuilder.Build((612, 792)));
        try
        {
            using var document = PdfDocumentHost.Open(path);

            document.PageCount.Should().Be(1);
            document.TryGetPageSize(1, out var size).Should().BeTrue();
            size.Width.Should().BeApproximately(612, 0.1f);
            size.Height.Should().BeApproximately(792, 0.1f);

            var at96Dpi = document.RenderPage(1, 96);
            at96Dpi.Should().NotBeNull();
            at96Dpi!.PixelWidth.Should().Be(816);
            at96Dpi.PixelHeight.Should().Be(1056);
            at96Dpi.IsFrozen.Should().BeTrue();

            var at144Dpi = document.RenderPage(1, 144);
            at144Dpi.Should().NotBeNull();
            at144Dpi!.PixelWidth.Should().Be(1224);
            at144Dpi.PixelHeight.Should().Be(1584);
            at144Dpi.IsFrozen.Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RenderPage_ShouldContainWhiteBackgroundAndDarkContent()
    {
        var path = WritePdf(MinimalPdfFixtureBuilder.Build((612, 792)));
        try
        {
            using var document = PdfDocumentHost.Open(path);
            var rendered = document.RenderPage(1, 96);

            rendered.Should().NotBeNull();
            var converted = new FormatConvertedBitmap(rendered!, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            ContainsPixel(pixels, IsNearlyWhite).Should().BeTrue();
            ContainsPixel(pixels, IsNearlyBlack).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_ShouldRejectCorruptPdfWithoutKeepingFileLocked()
    {
        var path = WritePdf(Encoding.ASCII.GetBytes("not a valid PDF"));
        try
        {
            var open = () => PdfDocumentHost.Open(path);

            open.Should().Throw<Exception>();
            File.Delete(path);
            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LargePageSet_ShouldExposeEveryPageAndLastPageGeometry()
    {
        var pages = Enumerable
            .Repeat((Width: 612d, Height: 792d), 127)
            .Append((Width: 300d, Height: 400d))
            .ToArray();
        var path = WritePdf(MinimalPdfFixtureBuilder.Build(pages));
        try
        {
            using var document = PdfDocumentHost.Open(path);

            document.PageCount.Should().Be(128);
            document.TryGetPageSize(128, out var size).Should().BeTrue();
            size.Width.Should().BeApproximately(300, 0.1f);
            size.Height.Should().BeApproximately(400, 0.1f);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RenderPage_ShouldFailClosedWhenRequestedBitmapExceedsSafetyBudget()
    {
        var path = WritePdf(MinimalPdfFixtureBuilder.Build((20_000, 20_000)));
        try
        {
            using var document = PdfDocumentHost.Open(path);

            document.TryGetPageSize(1, out var size).Should().BeTrue();
            size.Width.Should().BeGreaterThanOrEqualTo(14_000);
            size.Height.Should().BeGreaterThanOrEqualTo(14_000);
            document.RenderPage(1, 96).Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WritePdf(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"classroom-toolkit-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static bool ContainsPixel(byte[] pixels, Func<byte, byte, byte, byte, bool> predicate)
    {
        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (predicate(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNearlyWhite(byte blue, byte green, byte red, byte alpha)
        => alpha >= 250 && blue >= 245 && green >= 245 && red >= 245;

    private static bool IsNearlyBlack(byte blue, byte green, byte red, byte alpha)
        => alpha >= 250 && blue <= 10 && green <= 10 && red <= 10;
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PdfRenderingPerformanceCollection
{
    public const string Name = "PDF rendering performance";
}

[Collection(PdfRenderingPerformanceCollection.Name)]
[Trait("Gate", "Performance")]
public sealed class PdfDocumentHostPerformanceTests
{
    [Fact]
    public void RenderLetterPageAt96Dpi_ShouldStayWithinPreviewBudget()
    {
        var path = Path.Combine(Path.GetTempPath(), $"classroom-toolkit-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, MinimalPdfFixtureBuilder.Build((612, 792)));
        try
        {
            using var document = PdfDocumentHost.Open(path);
            document.RenderPage(1, 96).Should().NotBeNull();

            var stopwatch = Stopwatch.StartNew();
            for (var iteration = 0; iteration < 3; iteration++)
            {
                document.RenderPage(1, 96).Should().NotBeNull();
            }
            stopwatch.Stop();
            TestContext.Current.TestOutputHelper?.WriteLine(
                "Three warmed 96-DPI Letter renders: {0:F1} ms total, {1:F1} ms average.",
                stopwatch.Elapsed.TotalMilliseconds,
                stopwatch.Elapsed.TotalMilliseconds / 3);

            stopwatch.Elapsed.Should().BeLessThan(
                TimeSpan.FromSeconds(5),
                "three warmed Letter-page previews must not stall a classroom interaction");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

internal static class MinimalPdfFixtureBuilder
{
    private const string PageContent = "q 0 0 0 rg 72 72 144 144 re f Q\n";

    public static byte[] Build(params (double Width, double Height)[] pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Length == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        var pageObjectIds = Enumerable.Range(4, pages.Length).ToArray();
        var kids = string.Join(' ', pageObjectIds.Select(objectId => $"{objectId} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(PageContent)} >>\nstream\n{PageContent}endstream"
        };

        objects.AddRange(pages.Select(page =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {page.Width} {page.Height}] /Resources << >> /Contents 3 0 R >>")));

        return BuildDocument(objects);
    }

    private static byte[] BuildDocument(IReadOnlyList<string> objects)
    {
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder
                .Append(index + 1)
                .Append(" 0 obj\n")
                .Append(objects[index])
                .Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Count + 1).Append("\n");
        builder.Append("0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            builder.Append(offsets[index].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        builder
            .Append("trailer\n<< /Size ")
            .Append(objects.Count + 1)
            .Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset)
            .Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
