using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerCloseCallbackSafetyContractTests
{
    [Fact]
    public void BeginClose_ShouldIsolateLayoutCallbackFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth)");
        source.Should().Contain("ImageManager: close layout callback failed");
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
