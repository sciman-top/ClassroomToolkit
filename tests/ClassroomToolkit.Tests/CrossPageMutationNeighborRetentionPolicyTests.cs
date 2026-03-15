using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageMutationNeighborRetentionPolicyTests
{
    [Fact]
    public void ResolvePreservedPage_ShouldReturnPreviousPage_WhenClearAndPageChanged()
    {
        var preserved = CrossPageMutationNeighborRetentionPolicy.ResolvePreservedPage(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 5,
            currentPage: 6);

        preserved.Should().Be(5);
    }

    [Fact]
    public void ResolvePreservedPage_ShouldReturnZero_WhenPreviousPageInvalid()
    {
        var preserved = CrossPageMutationNeighborRetentionPolicy.ResolvePreservedPage(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 0,
            currentPage: 6);

        preserved.Should().Be(0);
    }

    [Fact]
    public void ResolvePreservedPage_ShouldReturnZero_WhenNotMutationClear()
    {
        var preserved = CrossPageMutationNeighborRetentionPolicy.ResolvePreservedPage(
            clearPreservedNeighborInkFrames: false,
            pageChanged: true,
            previousPage: 5,
            currentPage: 6);

        preserved.Should().Be(0);
    }
}
