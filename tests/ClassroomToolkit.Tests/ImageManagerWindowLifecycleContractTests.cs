using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerWindowLifecycleContractTests
{
    [Fact]
    public void OnWindowLoaded_ShouldRouteTreeInitializationThroughSafeTaskRunner()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_ = SafeTaskRunner.Run(");
        source.Should().Contain("\"ImageManagerWindow.InitializeTree\"");
        source.Should().Contain("InitializeTreeAsync,");
        source.Should().Contain("_lifecycleCancellation.Token,");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.Lifecycle.cs");
    }
}
