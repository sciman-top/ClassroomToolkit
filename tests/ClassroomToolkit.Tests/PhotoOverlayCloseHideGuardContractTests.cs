using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayCloseHideGuardContractTests
{
    [Fact]
    public void CloseOverlay_ShouldEnterTransparentPassthroughState_WithoutHide()
    {
        var source = File.ReadAllText(GetSourcePath());
        var closeStart = source.IndexOf("public void CloseOverlay()", StringComparison.Ordinal);
        var closeEnd = source.IndexOf("private void OnAutoCloseTick", StringComparison.Ordinal);
        closeStart.Should().BeGreaterThan(0);
        closeEnd.Should().BeGreaterThan(closeStart);

        var closeSource = source.Substring(closeStart, closeEnd - closeStart);
        var maskVisibleIndex = closeSource.IndexOf("LoadingMask.Visibility = Visibility.Visible;", StringComparison.Ordinal);
        var inactiveIndex = closeSource.IndexOf("EnterInactivePassthroughState();", StringComparison.Ordinal);

        maskVisibleIndex.Should().BeGreaterThan(0);
        inactiveIndex.Should().BeGreaterThan(0);
        maskVisibleIndex.Should().BeLessThan(inactiveIndex);
        closeSource.Should().NotContain("Hide();");
    }

    [Fact]
    public void InactivePassthroughState_ShouldDisableHitTestingAndEnableTransparentExtendedStyle()
    {
        var source = File.ReadAllText(GetSourcePath());
        var methodStart = source.IndexOf("private void EnterInactivePassthroughState()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void SetInputPassthrough(", methodStart, StringComparison.Ordinal);
        methodStart.Should().BeGreaterThan(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var methodSource = source.Substring(methodStart, methodEnd - methodStart);

        methodSource.Should().Contain("Opacity = 0.0;");
        methodSource.Should().Contain("IsHitTestVisible = false;");
        methodSource.Should().Contain("SetInputPassthrough(enabled: true);");
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
