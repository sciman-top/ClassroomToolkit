using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkShowTransitionCoordinatorTests
{
    [Fact]
    public void Apply_ShouldSkipEverything_WhenSettingDoesNotChange()
    {
        var setCount = 0;

        var result = InkShowTransitionCoordinator.Apply(
            currentInkShowEnabled: true,
            requestedEnabled: true,
            photoModeActive: true,
            setInkShowEnabled: _ => setCount++,
            purgePersistedInkForHiddenCurrentDocument: () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: () => throw new Xunit.Sdk.XunitException("should not clear surface"),
            clearNeighborInkVisuals: () => throw new Xunit.Sdk.XunitException("should not clear visuals"),
            clearNeighborInkCache: () => throw new Xunit.Sdk.XunitException("should not clear cache"),
            clearNeighborInkRenderPending: () => throw new Xunit.Sdk.XunitException("should not clear render pending"),
            clearNeighborInkSidecarLoadPending: () => throw new Xunit.Sdk.XunitException("should not clear sidecar pending"),
            loadCurrentPageIfExists: () => throw new Xunit.Sdk.XunitException("should not load"),
            requestCrossPageDisplayUpdate: _ => throw new Xunit.Sdk.XunitException("should not request update"));

        result.AppliedSetting.Should().BeFalse();
        setCount.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldOnlyUpdateSetting_WhenPhotoModeIsInactive()
    {
        var updatedValue = true;

        var result = InkShowTransitionCoordinator.Apply(
            currentInkShowEnabled: true,
            requestedEnabled: false,
            photoModeActive: false,
            setInkShowEnabled: nextEnabled => updatedValue = nextEnabled,
            purgePersistedInkForHiddenCurrentDocument: () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: () => throw new Xunit.Sdk.XunitException("should not clear surface"),
            clearNeighborInkVisuals: () => throw new Xunit.Sdk.XunitException("should not clear visuals"),
            clearNeighborInkCache: () => throw new Xunit.Sdk.XunitException("should not clear cache"),
            clearNeighborInkRenderPending: () => throw new Xunit.Sdk.XunitException("should not clear render pending"),
            clearNeighborInkSidecarLoadPending: () => throw new Xunit.Sdk.XunitException("should not clear sidecar pending"),
            loadCurrentPageIfExists: () => throw new Xunit.Sdk.XunitException("should not load"),
            requestCrossPageDisplayUpdate: _ => throw new Xunit.Sdk.XunitException("should not request update"));

        result.AppliedSetting.Should().BeTrue();
        result.ReturnedAfterSetting.Should().BeTrue();
        updatedValue.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldPurgeAndClearState_WhenDisablingInkShowInPhotoMode()
    {
        var purgeCount = 0;
        var clearSurfaceCount = 0;
        var clearVisualsCount = 0;
        var clearCacheCount = 0;
        var clearRenderPendingCount = 0;
        var clearSidecarPendingCount = 0;
        string? requestedSource = null;

        var result = InkShowTransitionCoordinator.Apply(
            currentInkShowEnabled: true,
            requestedEnabled: false,
            photoModeActive: true,
            setInkShowEnabled: _ => { },
            purgePersistedInkForHiddenCurrentDocument: () => purgeCount++,
            clearInkSurfaceState: () => clearSurfaceCount++,
            clearNeighborInkVisuals: () => clearVisualsCount++,
            clearNeighborInkCache: () => clearCacheCount++,
            clearNeighborInkRenderPending: () => clearRenderPendingCount++,
            clearNeighborInkSidecarLoadPending: () => clearSidecarPendingCount++,
            loadCurrentPageIfExists: () => throw new Xunit.Sdk.XunitException("should not load"),
            requestCrossPageDisplayUpdate: source => requestedSource = source);

        result.ClearedInkState.Should().BeTrue();
        result.RequestedCrossPageUpdate.Should().BeTrue();
        purgeCount.Should().Be(1);
        clearSurfaceCount.Should().Be(1);
        clearVisualsCount.Should().Be(1);
        clearCacheCount.Should().Be(1);
        clearRenderPendingCount.Should().Be(1);
        clearSidecarPendingCount.Should().Be(1);
        requestedSource.Should().Be(CrossPageUpdateSources.InkShowDisabled);
    }

    [Fact]
    public void Apply_ShouldLoadCurrentPageAndRequestUpdate_WhenEnablingInkShowInPhotoMode()
    {
        var loadCount = 0;
        string? requestedSource = null;

        var result = InkShowTransitionCoordinator.Apply(
            currentInkShowEnabled: false,
            requestedEnabled: true,
            photoModeActive: true,
            setInkShowEnabled: _ => { },
            purgePersistedInkForHiddenCurrentDocument: () => throw new Xunit.Sdk.XunitException("should not purge"),
            clearInkSurfaceState: () => throw new Xunit.Sdk.XunitException("should not clear surface"),
            clearNeighborInkVisuals: () => throw new Xunit.Sdk.XunitException("should not clear visuals"),
            clearNeighborInkCache: () => throw new Xunit.Sdk.XunitException("should not clear cache"),
            clearNeighborInkRenderPending: () => throw new Xunit.Sdk.XunitException("should not clear render pending"),
            clearNeighborInkSidecarLoadPending: () => throw new Xunit.Sdk.XunitException("should not clear sidecar pending"),
            loadCurrentPageIfExists: () => loadCount++,
            requestCrossPageDisplayUpdate: source => requestedSource = source);

        result.LoadedCurrentPage.Should().BeTrue();
        result.RequestedCrossPageUpdate.Should().BeTrue();
        loadCount.Should().Be(1);
        requestedSource.Should().Be(CrossPageUpdateSources.InkShowEnabled);
    }
}
