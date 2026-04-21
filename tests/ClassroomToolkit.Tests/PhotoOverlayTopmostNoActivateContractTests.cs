using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayTopmostNoActivateContractTests
{
    [Fact]
    public void Constructor_ShouldDisableActivation_WhenShowingPhotoOverlay()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("ShowActivated = false;");
        source.Should().NotContain("ShowActivated = true;");
    }

    [Fact]
    public void EnsureOverlayVisible_ShouldAvoidForcedZOrderReplay()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("WindowTopmostExecutor.ApplyNoActivate(this, enabled: false, enforceZOrder: false);");
    }

    [Fact]
    public void EnsureOverlayVisible_ShouldRequestImmediateMainWindowRetouch_WhenOverlayBecomesVisible()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var becameVisible = false;");
        source.Should().Contain("mainWindow.RequestImmediateFloatingZOrderRetouch();");
    }

    [Fact]
    public void Xaml_ShouldKeepPhotoOverlayBelowFloatingUtilitiesByDefault()
    {
        var xaml = File.ReadAllText(GetXamlPath());

        xaml.Should().Contain("Topmost=\"False\"");
        xaml.Should().NotContain("Topmost=\"True\"");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }

    private static string GetXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml");
    }
}
