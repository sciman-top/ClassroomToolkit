using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintWindowOrchestratorWhiteboardContractTests
{
    [Fact]
    public void WhiteboardEnter_ShouldNotForceExitPhotoMode()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private void OnToolbarWhiteboardToggled(bool active)");
        source.Should().Contain("OverlayWindow.SetBoardColor(_currentSettings.BoardColor);");
        source.Should().Contain("OverlayWindow.SetBoardOpacity(255);");
        source.Should().NotContain("OverlayWindow.ExitPhotoMode();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Services",
            "PaintWindowOrchestrator.cs");
    }
}
