using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayCloseHideGuardContractTests
{
    [Fact]
    public void CloseOverlay_ShouldEnterTransparentMaskedState_BeforeHide()
    {
        var source = File.ReadAllText(GetSourcePath());
        var closeStart = source.IndexOf("public void CloseOverlay()", StringComparison.Ordinal);
        var closeEnd = source.IndexOf("private void OnAutoCloseTick", StringComparison.Ordinal);
        closeStart.Should().BeGreaterThan(0);
        closeEnd.Should().BeGreaterThan(closeStart);

        var closeSource = source.Substring(closeStart, closeEnd - closeStart);
        var maskVisibleIndex = closeSource.IndexOf("LoadingMask.Visibility = Visibility.Visible;", StringComparison.Ordinal);
        var opacityZeroIndex = closeSource.IndexOf("Opacity = 0.0;", StringComparison.Ordinal);
        var hideIndex = closeSource.IndexOf("Hide();", StringComparison.Ordinal);

        maskVisibleIndex.Should().BeGreaterThan(0);
        opacityZeroIndex.Should().BeGreaterThan(0);
        hideIndex.Should().BeGreaterThan(0);
        maskVisibleIndex.Should().BeLessThan(hideIndex);
        opacityZeroIndex.Should().BeLessThan(hideIndex);
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
