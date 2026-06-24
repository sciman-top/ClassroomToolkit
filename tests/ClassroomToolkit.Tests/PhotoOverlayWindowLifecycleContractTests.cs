using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayWindowLifecycleContractTests
{
    [Fact]
    public void CloseOverlay_ShouldNoOp_WhenDispatcherIsShuttingDown()
    {
        var source = File.ReadAllText(GetSourcePath());
        var closeStart = source.IndexOf("public void CloseOverlay()", StringComparison.Ordinal);
        var requestIncrement = source.IndexOf("Interlocked.Increment(ref _photoLoadRequestId);", closeStart, StringComparison.Ordinal);

        closeStart.Should().BeGreaterThan(0);
        requestIncrement.Should().BeGreaterThan(closeStart);
        source.Substring(closeStart, requestIncrement - closeStart)
            .Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)");
    }

    [Fact]
    public void DeferredMaskHide_ShouldSkipInlineFallback_WhenDispatcherAlreadyShuttingDown()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)");
        source.Should().Contain("return;");
        source.Should().NotContain("HideMask();\r\n            return;");
    }

    [Fact]
    public void DeferredZOrderRetouch_ShouldGuardAgainstDispatcherShutdownInsideCallback()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)");
        source.Should().Contain("WindowTopmostExecutor.ApplyNoActivateBehind(this, _zOrderAnchor);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }
}
