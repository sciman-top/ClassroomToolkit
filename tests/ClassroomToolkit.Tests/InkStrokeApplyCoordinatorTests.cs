using System.Collections.Generic;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkStrokeApplyCoordinatorTests
{
    [Fact]
    public void Apply_ShouldRedraw_WhenFastPathIsNotApplied()
    {
        var clearCount = 0;
        var redrawCount = 0;
        var markLoadedCount = 0;
        var perfElapsed = 0d;
        var perfDispatcher = false;
        IReadOnlyList<InkStrokeData>? runtimeStrokes = null;
        var strokes = new List<InkStrokeData> { new(), new() };

        var result = InkStrokeApplyCoordinator.Apply(
            strokes: strokes,
            preferInteractiveFastPath: false,
            clearRuntimeStrokes: () => clearCount++,
            addRuntimeStrokes: added => runtimeStrokes = added,
            tryApplyNeighborInkBitmapForCurrentPage: static (_, _) => false,
            redrawInkSurface: () => redrawCount++,
            finalizeFastAppliedInkSurface: static () => { },
            markCurrentInkPageLoaded: () => markLoadedCount++,
            recordPerfMilliseconds: (elapsedMs, onDispatcher) =>
            {
                perfElapsed = elapsedMs;
                perfDispatcher = onDispatcher;
            },
            getElapsedMilliseconds: static () => 12.5,
            dispatcherCheckAccess: static () => true);

        result.AppliedStrokeCount.Should().Be(2);
        result.RedrewInkSurface.Should().BeTrue();
        result.UsedInteractiveFastPathCopy.Should().BeFalse();
        clearCount.Should().Be(1);
        redrawCount.Should().Be(1);
        markLoadedCount.Should().Be(1);
        runtimeStrokes.Should().BeSameAs(strokes);
        perfElapsed.Should().Be(12.5);
        perfDispatcher.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldSkipRedraw_WhenFastPathApplies()
    {
        var redrawCount = 0;
        var markLoadedCount = 0;
        var finalizeSurfaceCount = 0;

        var result = InkStrokeApplyCoordinator.Apply(
            strokes: new List<InkStrokeData> { new() },
            preferInteractiveFastPath: true,
            clearRuntimeStrokes: static () => { },
            addRuntimeStrokes: static _ => { },
            tryApplyNeighborInkBitmapForCurrentPage: static (_, interactiveSwitch) => interactiveSwitch,
            redrawInkSurface: () => redrawCount++,
            finalizeFastAppliedInkSurface: () => finalizeSurfaceCount++,
            markCurrentInkPageLoaded: () => markLoadedCount++,
            recordPerfMilliseconds: static (_, _) => { },
            getElapsedMilliseconds: static () => 3.2,
            dispatcherCheckAccess: static () => false);

        result.UsedInteractiveFastPathCopy.Should().BeTrue();
        result.RedrewInkSurface.Should().BeFalse();
        redrawCount.Should().Be(0);
        finalizeSurfaceCount.Should().Be(1);
        markLoadedCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldNotFinalizeFastAppliedSurface_WhenRedrawPathRuns()
    {
        var finalizeSurfaceCount = 0;

        InkStrokeApplyCoordinator.Apply(
            strokes: new List<InkStrokeData> { new() },
            preferInteractiveFastPath: false,
            clearRuntimeStrokes: static () => { },
            addRuntimeStrokes: static _ => { },
            tryApplyNeighborInkBitmapForCurrentPage: static (_, _) => false,
            redrawInkSurface: static () => { },
            finalizeFastAppliedInkSurface: () => finalizeSurfaceCount++,
            markCurrentInkPageLoaded: static () => { },
            recordPerfMilliseconds: static (_, _) => { },
            getElapsedMilliseconds: static () => 2.4,
            dispatcherCheckAccess: static () => true);

        finalizeSurfaceCount.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldEmitTraceStages_ForRedrawPath()
    {
        var traces = new List<string>();

        InkStrokeApplyCoordinator.Apply(
            strokes: new List<InkStrokeData> { new() },
            preferInteractiveFastPath: false,
            clearRuntimeStrokes: static () => { },
            addRuntimeStrokes: static _ => { },
            tryApplyNeighborInkBitmapForCurrentPage: static (_, _) => false,
            redrawInkSurface: static () => { },
            finalizeFastAppliedInkSurface: static () => { },
            markCurrentInkPageLoaded: static () => { },
            recordPerfMilliseconds: static (_, _) => { },
            getElapsedMilliseconds: static () => 8.8,
            dispatcherCheckAccess: static () => true,
            markTraceStage: (stage, detail) => traces.Add($"{stage}:{detail}"));

        traces.Should().Contain(t => t.StartsWith("apply-enter:"));
        traces.Should().Contain("apply-redraw:");
        traces.Should().Contain("apply-exit:ms=8.80");
    }
}
