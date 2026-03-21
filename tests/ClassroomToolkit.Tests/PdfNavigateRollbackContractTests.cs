using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfNavigateRollbackContractTests
{
    [Fact]
    public void NavigateToPage_ShouldRollbackPdfPageState_WhenRenderFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var previousCacheKey = _currentCacheKey;");
        source.Should().Contain("var previousTranslateY = _photoTranslate.Y;");
        source.Should().Contain("if (!RenderPdfPage(_currentPageIndex, interactiveSwitch, preloadedBitmap))");
        source.Should().Contain("_currentPageIndex = beforeCurrentPage;");
        source.Should().Contain("_currentCacheKey = previousCacheKey;");
        source.Should().Contain("_photoTranslate.Y = previousTranslateY;");
        source.Should().Contain("MarkCrossPageFirstInputStage(");
        source.Should().Contain("\"navigate-rollback\"");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation.cs");
    }
}
