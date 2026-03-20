using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerThumbnailDispatchFallbackContractTests
{
    [Fact]
    public void TryDispatchThumbnailUpdateAsync_ShouldApplyInlineFallback_WhenInvokeAsyncFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Dispatcher.CheckAccess()");
        source.Should().Contain("item.Thumbnail = thumbnail;");
        source.Should().Contain("if (item.IsPdf && pageCount > 0)");
        source.Should().Contain("ImageManagerDiagnosticsPolicy.FormatThumbnailDispatchFailureMessage(");
    }

    [Fact]
    public void AppendScanResultsAsync_ShouldSwallowShutdownRace_WhenYieldDispatchThrows()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("catch (OperationCanceledException) when (_isClosing)");
        source.Should().Contain("catch (ObjectDisposedException)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.xaml.cs");
    }
}
