using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayWhiteboardPhotoCacheContractTests
{
    [Fact]
    public void ExitPhotoMode_ShouldClearBoardSuspendedPhotoCacheFlag()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation*.cs");

        source.Should().Contain("public void ExitPhotoMode()");
        source.Should().Contain("_photoModeActive = false;");
        source.Should().Contain("_boardSuspendedPhotoCache = false;");
    }
}
