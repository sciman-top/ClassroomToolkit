using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayLoadBitmapDecodeContractTests
{
    [Fact]
    public void LoadBitmap_ShouldAvoidIgnoreImageCache_AndApplyDecodePixelWidth()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("bitmap.DecodePixelWidth = decodePixelWidth;");
        source.Should().Contain("ResolveDecodePixelWidth()");
        source.Should().NotContain("BitmapCreateOptions.IgnoreImageCache");
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
