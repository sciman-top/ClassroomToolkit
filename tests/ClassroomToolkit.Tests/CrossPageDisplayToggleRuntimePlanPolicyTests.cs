using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayToggleRuntimePlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableUnifiedRestore_WhenPhotoInkCrossPageAndUnifiedReady()
    {
        var plan = CrossPageDisplayToggleRuntimePlanPolicy.Resolve(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true,
            photoDocumentIsPdf: false,
            photoUnifiedTransformReady: true);

        plan.ShouldRestoreUnifiedTransformAndRedraw.Should().BeTrue();
        plan.ShouldSaveUnifiedTransformState.Should().BeFalse();
        plan.ShouldResetReplayAndClearNeighbors.Should().BeFalse();
        plan.ShouldRefreshImageSequenceSource.Should().BeTrue();
        plan.ShouldReloadPdfInkCache.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldEnableUnifiedSave_WhenPhotoInkCrossPageAndUnifiedNotReady()
    {
        var plan = CrossPageDisplayToggleRuntimePlanPolicy.Resolve(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true,
            photoDocumentIsPdf: true,
            photoUnifiedTransformReady: false);

        plan.ShouldRestoreUnifiedTransformAndRedraw.Should().BeFalse();
        plan.ShouldSaveUnifiedTransformState.Should().BeTrue();
        plan.ShouldResetReplayAndClearNeighbors.Should().BeFalse();
        plan.ShouldRefreshImageSequenceSource.Should().BeFalse();
        plan.ShouldReloadPdfInkCache.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldResetArtifacts_WhenCrossPageDisabled()
    {
        var plan = CrossPageDisplayToggleRuntimePlanPolicy.Resolve(
            photoInkModeActive: true,
            crossPageDisplayEnabled: false,
            photoDocumentIsPdf: false,
            photoUnifiedTransformReady: true);

        plan.ShouldRestoreUnifiedTransformAndRedraw.Should().BeFalse();
        plan.ShouldSaveUnifiedTransformState.Should().BeFalse();
        plan.ShouldResetReplayAndClearNeighbors.Should().BeTrue();
        plan.ShouldRefreshImageSequenceSource.Should().BeTrue();
        plan.ShouldReloadPdfInkCache.Should().BeFalse();
    }
}
