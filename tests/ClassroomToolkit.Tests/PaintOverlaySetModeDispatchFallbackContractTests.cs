using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlaySetModeDispatchFallbackContractTests
{
    [Fact]
    public void SetMode_ShouldFallbackInline_WhenDispatcherSchedulingFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var cursorUpdateScheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!cursorUpdateScheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("var modeFollowUpScheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!modeFollowUpScheduled && Dispatcher.CheckAccess())");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.xaml.cs");
    }
}
