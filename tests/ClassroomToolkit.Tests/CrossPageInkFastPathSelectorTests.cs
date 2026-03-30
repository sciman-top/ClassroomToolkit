using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInkFastPathSelectorTests
{
    [Fact]
    public void ShouldUseNeighborBitmapFastPath_ShouldReturnTrue_WhenInteractiveAndReferenceMatches()
    {
        var strokes = new List<InkStrokeData> { new() };

        var result = CrossPageInkFastPathSelector.ShouldUseNeighborBitmapFastPath(
            interactiveSwitch: true,
            currentPageStrokes: strokes,
            neighborCacheStrokes: strokes);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldUseNeighborBitmapFastPath_ShouldReturnFalse_WhenNotInteractive()
    {
        var strokes = new List<InkStrokeData> { new() };

        var result = CrossPageInkFastPathSelector.ShouldUseNeighborBitmapFastPath(
            interactiveSwitch: false,
            currentPageStrokes: strokes,
            neighborCacheStrokes: strokes);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldUseNeighborBitmapFastPath_ShouldReturnFalse_WhenReferenceDoesNotMatch()
    {
        var strokes = new List<InkStrokeData> { new() };
        var another = new List<InkStrokeData> { new() };

        var result = CrossPageInkFastPathSelector.ShouldUseNeighborBitmapFastPath(
            interactiveSwitch: true,
            currentPageStrokes: strokes,
            neighborCacheStrokes: another);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldUseNeighborBitmapFastPath_ShouldReturnFalse_WhenNoStrokes()
    {
        var empty = new List<InkStrokeData>();

        var result = CrossPageInkFastPathSelector.ShouldUseNeighborBitmapFastPath(
            interactiveSwitch: true,
            currentPageStrokes: empty,
            neighborCacheStrokes: empty);

        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateCandidateForRasterCopy_ShouldReturnApplied_WhenAllChecksPass()
    {
        var strokes = new List<InkStrokeData> { new() };

        var decision = CrossPageInkFastPathSelector.EvaluateCandidateForRasterCopy(
            interactiveSwitch: true,
            currentPageStrokes: strokes,
            candidateStrokes: strokes,
            candidatePixelWidth: 1920,
            candidatePixelHeight: 1080,
            candidateDpiX: 96,
            candidateDpiY: 96,
            surfacePixelWidth: 1920,
            surfacePixelHeight: 1080,
            surfaceDpiX: 96,
            surfaceDpiY: 96);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be("ok");
    }

    [Fact]
    public void EvaluateCandidateForRasterCopy_ShouldReject_WhenStrokeReferenceDiffers()
    {
        var strokes = new List<InkStrokeData> { new() };
        var another = new List<InkStrokeData> { new() };

        var decision = CrossPageInkFastPathSelector.EvaluateCandidateForRasterCopy(
            interactiveSwitch: true,
            currentPageStrokes: strokes,
            candidateStrokes: another,
            candidatePixelWidth: 1920,
            candidatePixelHeight: 1080,
            candidateDpiX: 96,
            candidateDpiY: 96,
            surfacePixelWidth: 1920,
            surfacePixelHeight: 1080,
            surfaceDpiX: 96,
            surfaceDpiY: 96);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be("stroke-reference-mismatch");
    }

    [Fact]
    public void EvaluateCandidateForRasterCopy_ShouldReject_WhenBitmapAndSurfaceSizeMismatch()
    {
        var strokes = new List<InkStrokeData> { new() };

        var decision = CrossPageInkFastPathSelector.EvaluateCandidateForRasterCopy(
            interactiveSwitch: true,
            currentPageStrokes: strokes,
            candidateStrokes: strokes,
            candidatePixelWidth: 1280,
            candidatePixelHeight: 720,
            candidateDpiX: 96,
            candidateDpiY: 96,
            surfacePixelWidth: 1920,
            surfacePixelHeight: 1080,
            surfaceDpiX: 96,
            surfaceDpiY: 96);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be("size-mismatch");
    }
}
