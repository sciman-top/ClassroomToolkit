using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageMutationNeighborSeedPolicyTests
{
    [Fact]
    public void ShouldSeedPreviousPageAfterClear_ShouldReturnTrue_WhenPageChangedAndClearWasRequested()
    {
        var shouldSeed = CrossPageMutationNeighborSeedPolicy.ShouldSeedPreviousPageAfterClear(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 3,
            currentPage: 4);

        shouldSeed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSeedPreviousPageAfterClear_ShouldReturnFalse_WhenNoClearWasRequested()
    {
        var shouldSeed = CrossPageMutationNeighborSeedPolicy.ShouldSeedPreviousPageAfterClear(
            clearPreservedNeighborInkFrames: false,
            pageChanged: true,
            previousPage: 3,
            currentPage: 4);

        shouldSeed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSeedPreviousPageAfterClear_ShouldReturnFalse_WhenPreviousPageIsInvalid()
    {
        var shouldSeed = CrossPageMutationNeighborSeedPolicy.ShouldSeedPreviousPageAfterClear(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 0,
            currentPage: 4);

        shouldSeed.Should().BeFalse();
    }
}
