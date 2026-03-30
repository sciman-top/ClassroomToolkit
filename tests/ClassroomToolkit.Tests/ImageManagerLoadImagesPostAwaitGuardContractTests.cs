using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerLoadImagesPostAwaitGuardContractTests
{
    [Fact]
    public void LoadImagesAsync_ShouldRecheckRequestAndCancellation_AfterAppendAwait()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("await AppendScanResultsAsync(result, token, requestId);");
        source.Should().Contain("if (token.IsCancellationRequested");
        source.Should().Contain("|| requestId != Volatile.Read(ref _loadImagesRequestId)");
        source.Should().Contain("|| _isClosing)");
        source.Should().Contain("ViewModel.CurrentIndex = GetNavigableItems().Count > 0 ? 0 : -1;");
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
