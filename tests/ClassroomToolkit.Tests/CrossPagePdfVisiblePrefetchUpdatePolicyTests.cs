using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePdfVisiblePrefetchUpdatePolicyTests
{
    [Fact]
    public void ShouldRefreshCrossPageDisplay_ShouldReturnTrue_WhenPdfAndCrossPagePhotoTransformActive()
    {
        CrossPagePdfVisiblePrefetchUpdatePolicy.ShouldRefreshCrossPageDisplay(
            photoModeActive: true,
            photoDocumentIsPdf: true,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldRefreshCrossPageDisplay_ShouldReturnFalse_WhenNotPdf()
    {
        CrossPagePdfVisiblePrefetchUpdatePolicy.ShouldRefreshCrossPageDisplay(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldRefreshCrossPageDisplay_ShouldReturnFalse_WhenBoardActive()
    {
        CrossPagePdfVisiblePrefetchUpdatePolicy.ShouldRefreshCrossPageDisplay(
            photoModeActive: true,
            photoDocumentIsPdf: true,
            boardActive: true,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }
}
