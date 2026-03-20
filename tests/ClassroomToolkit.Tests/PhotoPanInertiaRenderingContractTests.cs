using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanInertiaRenderingContractTests
{
    [Fact]
    public void PhotoPanInertia_ShouldUseCompositionRenderingLoop()
    {
        var source = File.ReadAllText(GetTransformSourcePath());

        source.Should().Contain("CompositionTarget.Rendering += OnPhotoPanInertiaRendering;");
        source.Should().Contain("CompositionTarget.Rendering -= OnPhotoPanInertiaRendering;");
        source.Should().NotContain("_photoPanInertiaTimer = new DispatcherTimer");
    }

    private static string GetTransformSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform.cs");
    }
}

