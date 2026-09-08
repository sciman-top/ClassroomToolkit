using System.IO;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerWindowLifecycleContractTests
{
    [Fact]
    public void OnWindowLoaded_ShouldStartTreeInitializationOnTheDispatcher()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_ = InitializeTreeAsync(_lifecycleCancellation.Token);");
        source.Should().Contain("wrapping");
        source.Should().Contain("DispatcherObject affinity");
        source.Should().Contain("_lifecycleCancellation.Token);");
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
