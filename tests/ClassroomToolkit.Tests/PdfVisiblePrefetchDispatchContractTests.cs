using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfVisiblePrefetchDispatchContractTests
{
    [Fact]
    public void OnPdfVisiblePrefetchCompleted_ShouldFallbackInline_WhenDispatchUnavailable()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var scheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("if (ShouldRefreshCrossPagePdfDisplay())");
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
