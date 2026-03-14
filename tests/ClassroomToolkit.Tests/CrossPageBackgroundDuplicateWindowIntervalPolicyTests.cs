using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBackgroundDuplicateWindowIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldIncreaseForNeighborMissing()
    {
        CrossPageBackgroundDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.NeighborMissing,
            defaultMs: 24).Should().Be(CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborMissingMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForNeighborSidecar()
    {
        CrossPageBackgroundDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.NeighborSidecar,
            defaultMs: 24).Should().Be(CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborSidecarMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForNeighborRender()
    {
        CrossPageBackgroundDuplicateWindowIntervalPolicy.ResolveMs(
            CrossPageUpdateSources.NeighborRender,
            defaultMs: 24).Should().Be(CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborRenderMs);
    }
}
