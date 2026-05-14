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

    [Fact]
    public void RequestInkRedraw_ShouldShareScheduledExecutionPath()
    {
        var source = File.ReadAllText(GetRenderingSourcePath());

        source.Should().Contain("private void RunPendingInkRedraw()");
        ContractSourceAggregateLoader.CountOccurrences(source, "RunPendingInkRedraw();").Should().Be(2);
        ContractSourceAggregateLoader.CountOccurrences(source, "RedrawInkSurface();").Should().Be(1);
        ContractSourceAggregateLoader.CountOccurrences(source, "OnInkRedrawCompleted();").Should().Be(1);
    }

    [Fact]
    public void RenderingCache_ShouldStayInDedicatedPartialFile()
    {
        var renderingSource = File.ReadAllText(GetRenderingSourcePath());
        var cacheSource = File.ReadAllText(GetRenderingCacheSourcePath());

        renderingSource.Should().NotContain("private readonly struct InkPenCacheKey");
        renderingSource.Should().NotContain("private SolidColorBrush GetCachedSolidBrush(");
        cacheSource.Should().Contain("private readonly struct DrawCommand");
        cacheSource.Should().Contain("private readonly struct InkPenCacheKey");
        cacheSource.Should().Contain("private SolidColorBrush GetCachedSolidBrush(");
        cacheSource.Should().Contain("private MediaPen GetCachedPen(");
    }

    private static string GetRenderingSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.Rendering.cs");
    }

    private static string GetRenderingCacheSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.Rendering.Cache.cs");
    }
}
