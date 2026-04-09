using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoOverlayWindowXamlLayoutContractTests
{
    [Fact]
    public void PhotoOverlayWindow_ShouldUseWindowedLayout_AndKeepThreeCloseButtons()
    {
        var xaml = File.ReadAllText(GetPhotoOverlayWindowXamlPath());

        xaml.Should().Contain("WindowStartupLocation=\"Manual\"");
        xaml.Should().NotContain("WindowState=\"Maximized\"");
        xaml.Should().Contain("x:Name=\"CloseButtonLeft\"");
        xaml.Should().Contain("x:Name=\"CloseButtonCenter\"");
        xaml.Should().Contain("x:Name=\"CloseButtonRight\"");
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
