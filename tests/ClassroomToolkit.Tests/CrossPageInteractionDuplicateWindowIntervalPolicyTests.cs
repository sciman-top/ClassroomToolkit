using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractionDuplicateWindowIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldIncreaseForPhotoPanLikeSources()
    {
        CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.PhotoPan,
            defaultMs: 8).Should().Be(CrossPageInteractionDuplicateWindowIntervalThresholds.PhotoPanLikeMs);
        CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.ManipulationDelta,
            defaultMs: 8).Should().Be(CrossPageInteractionDuplicateWindowIntervalThresholds.PhotoPanLikeMs);
        CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.StepViewport,
            defaultMs: 8).Should().Be(CrossPageInteractionDuplicateWindowIntervalThresholds.PhotoPanLikeMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForPointerUpFast()
    {
        CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.PointerUpFast,
            defaultMs: 8).Should().Be(CrossPageInteractionDuplicateWindowIntervalThresholds.PointerUpFastMs);
    }

    [Fact]
    public void ResolveMs_ShouldKeepDefaultForOtherSources()
    {
        CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.ApplyScale,
            defaultMs: 8).Should().Be(8);
    }
}
