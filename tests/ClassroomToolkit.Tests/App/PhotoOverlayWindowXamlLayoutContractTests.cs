using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoOverlayWindowXamlLayoutContractTests
{
    [Fact]
    public void PhotoOverlayWindow_ShouldUseWindowedLayout_AndAvoidOverlayCloseButton()
    {
        var xaml = File.ReadAllText(GetPhotoOverlayWindowXamlPath());

        xaml.Should().Contain("WindowStartupLocation=\"Manual\"");
        xaml.Should().NotContain("WindowState=\"Maximized\"");
        xaml.Should().NotContain("x:Name=\"CloseButton\"");
        xaml.Should().NotContain("Click=\"OnCloseClick\"");
        xaml.Should().NotContain("Style_OverlayShellCloseButton");
        xaml.Should().NotContain("MouseLeftButtonDown=\"OnCloseClick\"");
        xaml.Should().Contain("MouseLeftButtonDown=\"OnPhotoImageMouseLeftButtonDown\"");
        xaml.Should().NotContain("x:Name=\"CloseButtonLeft\"");
        xaml.Should().NotContain("x:Name=\"CloseButtonCenter\"");
        xaml.Should().NotContain("x:Name=\"CloseButtonRight\"");
    }

    private static string GetPhotoOverlayWindowXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml");
    }
}
