using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayDeferredBoundsRecoveryContractTests
{
    [Fact]
    public void RequestDeferredFullscreenBoundsRecovery_ShouldFallbackInline_WhenDispatchUnavailable()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var scheduled = TryBeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ApplyFullscreenBounds();");
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
