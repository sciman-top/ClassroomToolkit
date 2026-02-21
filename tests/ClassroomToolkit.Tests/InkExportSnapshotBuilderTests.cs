using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;

namespace ClassroomToolkit.Tests;

public sealed class InkExportSnapshotBuilderTests
{
    [Fact]
    public void TryParseCacheKey_ShouldParsePdfAndImageKeys()
    {
        InkExportSnapshotBuilder.TryParseCacheKey("img|E:\\a.png", out var imgPath, out var imgPage).Should().BeTrue();
        imgPath.Should().Be("E:\\a.png");
        imgPage.Should().Be(1);

        InkExportSnapshotBuilder.TryParseCacheKey("pdf|E:\\a.pdf|page_003", out var pdfPath, out var pdfPage).Should().BeTrue();
        pdfPath.Should().Be("E:\\a.pdf");
        pdfPage.Should().Be(3);
    }

    [Fact]
    public void ApplyScopeFilter_ShouldKeepOnlySelectedPages_WhenSessionScope()
    {
        var doc = new InkDocumentData
        {
            SourcePath = "x.pdf",
            Pages = new List<InkPageData>
            {
                new() { PageIndex = 1, Strokes = new List<InkStrokeData> { new() } },
                new() { PageIndex = 2, Strokes = new List<InkStrokeData> { new() } }
            }
        };

        InkExportSnapshotBuilder.ApplyScopeFilter(
            doc,
            InkExportScope.SessionChangesOnly,
            page => page.PageIndex == 2);

        doc.Pages.Should().ContainSingle();
        doc.Pages[0].PageIndex.Should().Be(2);
    }

    [Fact]
    public void MergeCachedPages_ShouldUpsertMatchingSourceOnly()
    {
        var doc = new InkDocumentData { SourcePath = "E:\\a.pdf" };
        var cache = new List<(string Key, List<InkStrokeData> Strokes)>
        {
            ("pdf|E:\\a.pdf|page_001", new List<InkStrokeData> { new() }),
            ("pdf|E:\\b.pdf|page_001", new List<InkStrokeData> { new(), new() })
        };

        InkExportSnapshotBuilder.MergeCachedPages(
            doc,
            "E:\\a.pdf",
            cache,
            source => source.ToList());

        doc.Pages.Should().ContainSingle();
        doc.Pages[0].PageIndex.Should().Be(1);
        doc.Pages[0].Strokes.Should().HaveCount(1);
    }
}
