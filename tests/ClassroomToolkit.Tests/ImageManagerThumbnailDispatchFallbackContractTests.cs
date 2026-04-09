using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerThumbnailDispatchFallbackContractTests
{
    [Fact]
    public void TryDispatchThumbnailUpdateAsync_ShouldApplyInlineFallback_WhenInvokeAsyncFailsOnUiThread()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow*.cs");

        source.Should().Contain("if (Dispatcher.CheckAccess()");
        source.Should().Contain("item.Thumbnail = thumbnail;");
        source.Should().Contain("if (item.IsPdf && pageCount > 0)");
        source.Should().Contain("ImageManagerDiagnosticsPolicy.FormatThumbnailDispatchFailureMessage(");
    }

    [Fact]
    public void AppendScanResultsAsync_ShouldSwallowShutdownRace_WhenYieldDispatchThrows()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow*.cs");

        source.Should().Contain("catch (OperationCanceledException) when (_isClosing)");
        source.Should().Contain("catch (ObjectDisposedException)");
    }
}
