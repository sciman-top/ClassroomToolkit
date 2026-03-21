using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowPhotoSurfaceTransitionContractTests
{
    [Fact]
    public void ApplyPhotoModeSurfaceTransition_ShouldUseContextParameter_InsteadOfBooleanTuple()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private void ApplyPhotoModeSurfaceTransition(");
        source.Should().Contain("PhotoModeSurfaceTransitionContext context)");
    }

    [Fact]
    public void PhotoModeChangedAndPresentationFullscreenPaths_ShouldBuildSurfaceTransitionContext()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var context = new PhotoModeSurfaceTransitionContext(");
        source.Should().Contain("PhotoModeSurfaceTransitionKind.PhotoModeChanged");
        source.Should().Contain("PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Photo.cs");
    }
}
