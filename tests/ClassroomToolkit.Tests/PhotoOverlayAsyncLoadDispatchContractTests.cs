using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayAsyncLoadDispatchContractTests
{
    [Fact]
    public void ShowPhotoAsyncLoad_ShouldFallbackInline_WhenInvokeAsyncSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ApplyLoadedBitmapOnUi()");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("await Dispatcher.InvokeAsync(ApplyLoadedBitmapOnUi, DispatcherPriority.Background);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ApplyLoadedBitmapOnUi();");
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
