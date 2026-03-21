using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDelayedDispatchCancellationContractTests
{
    [Fact]
    public void DelayedDispatchCancellation_ShouldRouteThroughUiThreadFailureHandler()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (string.IsNullOrWhiteSpace(delayOutcome.FailureDetail))");
        source.Should().Contain("mode: \"delayed-canceled\"");
        source.Should().Contain("abortDetail: \"delayed-canceled-dispatch-unavailable\"");
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
