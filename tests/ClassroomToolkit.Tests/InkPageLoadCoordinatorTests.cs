using System.Collections.Generic;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkPageLoadCoordinatorTests
{
    [Fact]
    public void Apply_ShouldSkip_WhenPhotoScopeIsInactive()
    {
        var traces = new List<string>();

        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: false,
            inkCacheEnabled: true,
            inkShowEnabled: true,
            currentCacheKey: "photo|1",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: static () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: static () => throw new Xunit.Sdk.XunitException("should not clear"),
            applyInkStrokes: static (_, _) => throw new Xunit.Sdk.XunitException("should not apply"),
            markTraceStage: (stage, detail) => traces.Add($"{stage}:{detail}"));

        result.SkippedForNonPhotoScope.Should().BeTrue();
        traces.Should().Contain("load-skip:scope!=photo");
    }

    [Fact]
    public void Apply_ShouldClear_WhenCacheIsDisabled()
    {
        var clearCount = 0;

        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: false,
            inkShowEnabled: true,
            currentCacheKey: "photo|1",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: static () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: () => clearCount++,
            applyInkStrokes: static (_, _) => throw new Xunit.Sdk.XunitException("should not apply"));

        result.ClearedInkState.Should().BeTrue();
        clearCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldPurgeAndClear_WhenInkShowIsDisabled()
    {
        var purgeCount = 0;
        var clearCount = 0;

        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: true,
            inkShowEnabled: false,
            currentCacheKey: "photo|1",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: () => purgeCount++,
            clearInkSurfaceState: () => clearCount++,
            applyInkStrokes: static (_, _) => throw new Xunit.Sdk.XunitException("should not apply"));

        result.PurgedHiddenCurrentPage.Should().BeTrue();
        result.ClearedInkState.Should().BeTrue();
        purgeCount.Should().Be(1);
        clearCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldSkip_WhenCacheKeyIsEmpty()
    {
        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: true,
            inkShowEnabled: true,
            currentCacheKey: "",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: static () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: static () => throw new Xunit.Sdk.XunitException("should not clear"),
            applyInkStrokes: static (_, _) => throw new Xunit.Sdk.XunitException("should not apply"));

        result.SkippedForEmptyCacheKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldUseCachedStrokes_WhenCacheHits()
    {
        var applyCount = 0;
        IReadOnlyList<InkStrokeData>? appliedStrokes = null;
        var cached = new List<InkStrokeData> { new(), new() };

        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: true,
            inkShowEnabled: true,
            currentCacheKey: "photo|1",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: true,
            tryGetCachedStrokes: (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = cached;
                return true;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: static () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: static () => throw new Xunit.Sdk.XunitException("should not clear"),
            applyInkStrokes: (strokes, fastPath) =>
            {
                fastPath.Should().BeTrue();
                applyCount++;
                appliedStrokes = strokes;
            });

        result.AppliedCachedStrokes.Should().BeTrue();
        result.LoadedStrokeCount.Should().Be(2);
        applyCount.Should().Be(1);
        appliedStrokes.Should().BeSameAs(cached);
    }

    [Fact]
    public void Apply_ShouldUseSidecar_WhenCacheMissesAndFallbackLoads()
    {
        var sidecarCount = 0;

        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: true,
            inkShowEnabled: true,
            currentCacheKey: "photo|1",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: () =>
            {
                sidecarCount++;
                return true;
            },
            purgePersistedInkForHiddenCurrentPage: static () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: static () => throw new Xunit.Sdk.XunitException("should not clear"),
            applyInkStrokes: static (_, _) => throw new Xunit.Sdk.XunitException("should not apply"));

        result.LoadedFromSidecar.Should().BeTrue();
        sidecarCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldClear_WhenCacheMissesAndSidecarDoesNotLoad()
    {
        var clearCount = 0;

        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: true,
            inkShowEnabled: true,
            currentCacheKey: "photo|1",
            allowDiskFallback: true,
            hasInkPersistence: true,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: static () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: () => clearCount++,
            applyInkStrokes: static (_, _) => throw new Xunit.Sdk.XunitException("should not apply"));

        result.ClearedInkState.Should().BeTrue();
        clearCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldIgnoreTraceCallbackFailures()
    {
        Action act = () => InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: true,
            inkCacheEnabled: true,
            inkShowEnabled: true,
            currentCacheKey: "photo|trace",
            allowDiskFallback: true,
            hasInkPersistence: false,
            preferInteractiveFastPath: false,
            tryGetCachedStrokes: static (string _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                return false;
            },
            tryLoadInkFromSidecar: static () => false,
            purgePersistedInkForHiddenCurrentPage: static () => { },
            clearInkSurfaceState: static () => { },
            applyInkStrokes: static (_, _) => { },
            markTraceStage: static (_, _) => throw new InvalidOperationException("trace failed"));

        act.Should().NotThrow();
    }
}
