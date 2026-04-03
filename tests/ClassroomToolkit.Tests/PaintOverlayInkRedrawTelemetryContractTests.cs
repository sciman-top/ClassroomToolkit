using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayInkRedrawTelemetryContractTests
{
    [Fact]
    public void TrackInkRedrawTelemetry_ShouldRouteThroughInkRuntimeDiagnostics()
    {
        var source = File.ReadAllText(GetRenderingSourcePath());

        source.Should().Contain("_inkDiagnostics?.OnInkRedrawTelemetry(");
        source.Should().NotContain("Debug.WriteLine(\"[InkRedrawTelemetry]");
    }

    private static string GetRenderingSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.Rendering.cs");
    }
}
