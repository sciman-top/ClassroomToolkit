using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayLoadedBitmapDispatchContractTests
{
    [Fact]
    public void ApplyLoadedBitmap_ShouldFallbackInline_WhenDispatcherSchedulingFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("new Action(ApplyOverlayLayoutAfterPhotoLoad)");
        source.Should().Contain("if (!scheduled)");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("ApplyOverlayLayoutAfterPhotoLoad();");
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
