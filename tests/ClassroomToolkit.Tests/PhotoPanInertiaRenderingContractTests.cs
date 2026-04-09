using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanInertiaRenderingContractTests
{
    [Fact]
    public void PhotoPanInertia_ShouldUseCompositionRenderingLoop()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform*.cs");

        source.Should().Contain("CompositionTarget.Rendering += OnPhotoPanInertiaRendering;");
        source.Should().Contain("CompositionTarget.Rendering -= OnPhotoPanInertiaRendering;");
        source.Should().NotContain("_photoPanInertiaTimer = new DispatcherTimer");
    }
}
