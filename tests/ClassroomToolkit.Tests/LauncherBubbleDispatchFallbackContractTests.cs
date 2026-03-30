using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherBubbleDispatchFallbackContractTests
{
    [Fact]
    public void MouseUpSnap_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void SnapBubbleToNearestEdge()");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("new Action(SnapBubbleToNearestEdge)");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("SnapBubbleToNearestEdge();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "LauncherBubbleWindow.xaml.cs");
    }
}
