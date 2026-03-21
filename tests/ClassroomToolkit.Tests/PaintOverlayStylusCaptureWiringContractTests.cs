using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayStylusCaptureWiringContractTests
{
    [Fact]
    public void OverlayRoot_ShouldSubscribeAndUnsubscribe_LostStylusCapture()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("OverlayRoot.LostStylusCapture += OnOverlayLostStylusCapture;");
        source.Should().Contain("OverlayRoot.LostStylusCapture -= OnOverlayLostStylusCapture;");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.xaml.cs");
    }
}
