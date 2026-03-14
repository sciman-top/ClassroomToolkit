using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayToggleTransitionCoordinatorTests
{
    [Fact]
    public void Apply_ShouldSkipEverything_WhenFlagDoesNotChange()
    {
        var setCount = 0;
        var resetWidthCount = 0;

        var result = CrossPageDisplayToggleTransitionCoordinator.Apply(
            currentCrossPageDisplayEnabled: true,
            requestedEnabled: true,
            photoInkModeActive: true,
            photoDocumentIsPdf: false,
            photoUnifiedTransformReady: true,
            setCrossPageDisplayEnabled: _ => setCount++,
            resetCrossPageNormalizedWidth: () => resetWidthCount++,
            restoreUnifiedTransformAndRedraw: () => throw new Xunit.Sdk.XunitException("should not restore"),
            saveUnifiedTransformState: () => throw new Xunit.Sdk.XunitException("should not save"),
            updateCurrentPageWidthNormalization: () => throw new Xunit.Sdk.XunitException("should not update width"),
            resetCrossPageReplayState: () => throw new Xunit.Sdk.XunitException("should not reset replay"),
            clearNeighborPages: () => throw new Xunit.Sdk.XunitException("should not clear neighbors"),
            refreshCurrentImageSequenceSourceAfterToggle: () => throw new Xunit.Sdk.XunitException("should not refresh image sequence"),
            reloadPdfInkCacheAfterToggle: () => throw new Xunit.Sdk.XunitException("should not reload pdf ink"));

        result.AppliedFlagUpdate.Should().BeFalse();
        setCount.Should().Be(0);
        resetWidthCount.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldRestoreUnifiedTransformAndRefreshImageSequence_WhenEnablingImageCrossPageWithUnifiedTransform()
    {
        var setValue = false;
        var resetWidthCount = 0;
        var restoreCount = 0;
        var refreshCount = 0;
        var widthNormalizationCount = 0;

        var result = CrossPageDisplayToggleTransitionCoordinator.Apply(
            currentCrossPageDisplayEnabled: false,
            requestedEnabled: true,
            photoInkModeActive: true,
            photoDocumentIsPdf: false,
            photoUnifiedTransformReady: true,
            setCrossPageDisplayEnabled: value => setValue = value,
            resetCrossPageNormalizedWidth: () => resetWidthCount++,
            restoreUnifiedTransformAndRedraw: () => restoreCount++,
            saveUnifiedTransformState: () => throw new Xunit.Sdk.XunitException("should not save"),
            updateCurrentPageWidthNormalization: () => widthNormalizationCount++,
            resetCrossPageReplayState: () => throw new Xunit.Sdk.XunitException("should not reset replay"),
            clearNeighborPages: () => throw new Xunit.Sdk.XunitException("should not clear neighbors"),
            refreshCurrentImageSequenceSourceAfterToggle: () => refreshCount++,
            reloadPdfInkCacheAfterToggle: () => throw new Xunit.Sdk.XunitException("should not reload pdf ink"));

        result.AppliedFlagUpdate.Should().BeTrue();
        result.ResetNormalizedWidth.Should().BeTrue();
        result.RestoredUnifiedTransformAndRedraw.Should().BeTrue();
        result.RefreshedImageSequenceSource.Should().BeTrue();
        result.SavedUnifiedTransformState.Should().BeFalse();
        result.ResetReplayAndClearedNeighbors.Should().BeFalse();
        setValue.Should().BeTrue();
        resetWidthCount.Should().Be(1);
        restoreCount.Should().Be(1);
        refreshCount.Should().Be(1);
        widthNormalizationCount.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldSaveTransformAndReloadPdfInk_WhenEnablingPdfCrossPageWithoutUnifiedTransform()
    {
        var saveCount = 0;
        var reloadPdfCount = 0;
        var widthNormalizationCount = 0;

        var result = CrossPageDisplayToggleTransitionCoordinator.Apply(
            currentCrossPageDisplayEnabled: false,
            requestedEnabled: true,
            photoInkModeActive: true,
            photoDocumentIsPdf: true,
            photoUnifiedTransformReady: false,
            setCrossPageDisplayEnabled: _ => { },
            resetCrossPageNormalizedWidth: () => { },
            restoreUnifiedTransformAndRedraw: () => throw new Xunit.Sdk.XunitException("should not restore"),
            saveUnifiedTransformState: () => saveCount++,
            updateCurrentPageWidthNormalization: () => widthNormalizationCount++,
            resetCrossPageReplayState: () => throw new Xunit.Sdk.XunitException("should not reset replay"),
            clearNeighborPages: () => throw new Xunit.Sdk.XunitException("should not clear neighbors"),
            refreshCurrentImageSequenceSourceAfterToggle: () => throw new Xunit.Sdk.XunitException("should not refresh image sequence"),
            reloadPdfInkCacheAfterToggle: () => reloadPdfCount++);

        result.SavedUnifiedTransformState.Should().BeTrue();
        result.ReloadedPdfInkCache.Should().BeTrue();
        saveCount.Should().Be(1);
        reloadPdfCount.Should().Be(1);
        widthNormalizationCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldResetReplayAndClearNeighbors_WhenDisablingCrossPageDisplay()
    {
        var replayResetCount = 0;
        var clearNeighborCount = 0;
        var widthNormalizationCount = 0;
        var refreshCount = 0;

        var result = CrossPageDisplayToggleTransitionCoordinator.Apply(
            currentCrossPageDisplayEnabled: true,
            requestedEnabled: false,
            photoInkModeActive: true,
            photoDocumentIsPdf: false,
            photoUnifiedTransformReady: true,
            setCrossPageDisplayEnabled: _ => { },
            resetCrossPageNormalizedWidth: () => { },
            restoreUnifiedTransformAndRedraw: () => throw new Xunit.Sdk.XunitException("should not restore"),
            saveUnifiedTransformState: () => throw new Xunit.Sdk.XunitException("should not save"),
            updateCurrentPageWidthNormalization: () => widthNormalizationCount++,
            resetCrossPageReplayState: () => replayResetCount++,
            clearNeighborPages: () => clearNeighborCount++,
            refreshCurrentImageSequenceSourceAfterToggle: () => refreshCount++,
            reloadPdfInkCacheAfterToggle: () => throw new Xunit.Sdk.XunitException("should not reload pdf ink"));

        result.ResetReplayAndClearedNeighbors.Should().BeTrue();
        replayResetCount.Should().Be(1);
        clearNeighborCount.Should().Be(1);
        widthNormalizationCount.Should().Be(1);
        refreshCount.Should().Be(1);
    }
}
