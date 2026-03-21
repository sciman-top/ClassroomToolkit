using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDispatchFailurePendingClearContractTests
{
    [Fact]
    public void DispatchFailureHandler_ShouldClearPendingAndRunRecovery_WhenUiDispatchUnavailable()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);");
        source.Should().Contain("HandleCrossPageDisplayUpdateDispatchFailure(");
        source.Should().Contain("_inkDiagnostics?.OnCrossPageUpdateEvent(\"defer-abort\", source, abortDetail);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage.Display.cs");
    }
}
