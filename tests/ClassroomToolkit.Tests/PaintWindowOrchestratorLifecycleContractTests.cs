using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintWindowOrchestratorLifecycleContractTests
{
    [Fact]
    public void EnsureWindows_ShouldClosePartialPair_BeforeRebuild()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (OverlayWindow != null || ToolbarWindow != null)");
        source.Should().Contain("Detected partial paint window lifecycle state. Rebuilding pair.");
        source.Should().Contain("Close();");
        source.Should().Contain("var windows = _paintWindowFactory.Create();");
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
