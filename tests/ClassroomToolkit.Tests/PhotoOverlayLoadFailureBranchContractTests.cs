using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayLoadFailureBranchContractTests
{
    [Fact]
    public void ApplyLoadedBitmap_WhenBitmapIsNull_ShouldHideOnFailure_WithoutImmediateCacheClear()
    {
        var source = File.ReadAllText(GetSourcePath());
        var applyStart = source.IndexOf("private void ApplyLoadedBitmap(", StringComparison.Ordinal);
        var nullBranchStart = source.IndexOf("if (bitmap == null)", applyStart, StringComparison.Ordinal);
        var nullBranchReturn = source.IndexOf("return;", nullBranchStart, StringComparison.Ordinal);

        applyStart.Should().BeGreaterThan(0);
        nullBranchStart.Should().BeGreaterThan(applyStart);
        nullBranchReturn.Should().BeGreaterThan(nullBranchStart);

        var nullBranch = source.Substring(nullBranchStart, nullBranchReturn - nullBranchStart);
        nullBranch.Should().Contain("_autoCloseTimer.Stop();");
        nullBranch.Should().Contain("LoadingMask.Visibility = Visibility.Collapsed;");
        nullBranch.Should().Contain("LoadingMask.Background = _defaultLoadingMaskBrush;");
        nullBranch.Should().Contain("if (hideWhenFailed)");
        nullBranch.Should().Contain("Hide();");
        nullBranch.Should().NotContain("ClearPhotoCache(");
        nullBranch.Should().NotContain("PhotoClosed?.Invoke");
    }

    [Fact]
    public void ApplyLoadedBitmap_NullBitmapBranch_ShouldKeepFailureTelemetry()
    {
        var source = File.ReadAllText(GetSourcePath());
        var applyStart = source.IndexOf("private void ApplyLoadedBitmap(", StringComparison.Ordinal);
        var nullBranchStart = source.IndexOf("if (bitmap == null)", applyStart, StringComparison.Ordinal);
        var nullBranchReturn = source.IndexOf("return;", nullBranchStart, StringComparison.Ordinal);

        applyStart.Should().BeGreaterThan(0);
        nullBranchStart.Should().BeGreaterThan(applyStart);
        nullBranchReturn.Should().BeGreaterThan(nullBranchStart);

        var nullBranch = source.Substring(nullBranchStart, nullBranchReturn - nullBranchStart);
        nullBranch.Should().Contain("\"apply-null\"");
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
