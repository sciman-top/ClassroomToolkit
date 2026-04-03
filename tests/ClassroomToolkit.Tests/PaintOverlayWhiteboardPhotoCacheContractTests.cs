using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayWhiteboardPhotoCacheContractTests
{
    [Fact]
    public void ExitPhotoMode_ShouldClearBoardSuspendedPhotoCacheFlag()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("public void ExitPhotoMode()");
        source.Should().Contain("_photoModeActive = false;");
        source.Should().Contain("_boardSuspendedPhotoCache = false;");
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
