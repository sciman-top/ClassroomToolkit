using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerWindowDispatchFallbackContractTests
{
    [Fact]
    public void DeferredWindowDispatches_ShouldFallbackInline_WhenSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ApplyDeferredSplitterUpdate()");
        source.Should().Contain("void ApplyRestoredBounds()");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.xaml.cs");
    }
}
