using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoFullscreenBoundsEnforcementContractTests
{
    [Fact]
    public void SchedulePhotoFullscreenBoundsEnforcement_ShouldHandleDispatchFailureFallback()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var scheduled = TryBeginInvoke(() =>");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("fullscreen-enforcement dispatch unavailable.");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation.cs");
    }
}
