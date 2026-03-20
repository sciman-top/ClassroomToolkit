using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayPresentationTargetRoutingContractTests
{
    [Fact]
    public void OverlayPresentationRouting_ShouldSendToResolvedTarget()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_presentationService.TrySendToTarget(target, command, options);");
        source.Should().NotContain("_presentationService.TrySendForeground(command, options);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
    }
}
