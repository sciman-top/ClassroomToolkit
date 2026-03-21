using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfPrefetchReplayContractTests
{
    [Fact]
    public void SchedulePdfPrefetch_ShouldReplayLatestRequest_WhenInFlightRequestBecomesStale()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("Volatile.Write(ref _pdfPrefetchRequestedPageIndex, pageIndex);");
        source.Should().Contain("Volatile.Write(ref _pdfPrefetchRequestedDirection, direction);");
        source.Should().Contain("var token = Interlocked.Increment(ref _pdfPrefetchToken);");
        source.Should().Contain("if (token == Volatile.Read(ref _pdfPrefetchToken) || !CanUsePdfDocument())");
        source.Should().Contain("SchedulePdfPrefetch(pendingPageIndex, pendingDirection);");
    }

    [Fact]
    public void SchedulePdfVisiblePrefetch_ShouldReplayLatestPageSet_WhenInFlightRequestBecomesStale()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("Volatile.Write(ref _pdfVisiblePrefetchRequestedPages, unique);");
        source.Should().Contain("if (token != Volatile.Read(ref _pdfVisiblePrefetchToken))");
        source.Should().Contain("SchedulePdfVisiblePrefetch(pendingPageIndexes);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Pdf.cs");
    }
}
