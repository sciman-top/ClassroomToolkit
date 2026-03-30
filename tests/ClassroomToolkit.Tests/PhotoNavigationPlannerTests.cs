using ClassroomToolkit.Application.UseCases.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationPlannerTests
{
    [Fact]
    public void Plan_ShouldNotNavigateFile_WhenCurrentIsPdf()
    {
        var sequence = new[] { "a.png", "deck.pdf", "b.png" };

        var decision = PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: sequence,
            CurrentIndex: 1,
            CurrentPath: "deck.pdf",
            Direction: 1));

        decision.ShouldNavigateFile.Should().BeFalse();
        decision.CurrentFileType.Should().Be(PhotoFileType.Pdf);
    }

    [Fact]
    public void Plan_ShouldSkipPdfAndNavigateToNextImage_WhenCurrentIsImage()
    {
        var sequence = new[] { "a.png", "deck.pdf", "b.jpg" };

        var decision = PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: sequence,
            CurrentIndex: 0,
            CurrentPath: "a.png",
            Direction: 1));

        decision.ShouldNavigateFile.Should().BeTrue();
        decision.NextIndex.Should().Be(2);
    }

    [Fact]
    public void Plan_ShouldUseCurrentPathToResolveIndex_WhenCurrentIndexIsStale()
    {
        var sequence = new[] { "a.png", "b.png", "c.png" };

        var decision = PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: sequence,
            CurrentIndex: 0,
            CurrentPath: "c.png",
            Direction: -1));

        decision.ResolvedCurrentIndex.Should().Be(2);
        decision.ShouldNavigateFile.Should().BeTrue();
        decision.NextIndex.Should().Be(1);
    }

    [Fact]
    public void Plan_ShouldNotNavigateFile_WhenNoImageExistsInDirection()
    {
        var sequence = new[] { "a.png", "deck.pdf" };

        var decision = PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: sequence,
            CurrentIndex: 0,
            CurrentPath: "a.png",
            Direction: 1));

        decision.ShouldNavigateFile.Should().BeFalse();
        decision.NextIndex.Should().Be(-1);
    }

    [Fact]
    public void Plan_ShouldNotNavigateFile_WhenDirectionIsZero()
    {
        var sequence = new[] { "a.png", "b.png" };

        var decision = PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: sequence,
            CurrentIndex: 0,
            CurrentPath: "a.png",
            Direction: 0));

        decision.ShouldNavigateFile.Should().BeFalse();
    }

    [Fact]
    public void Plan_ShouldRespectCurrentFileTypeHint_WhenPathIsImageButHintIsPdf()
    {
        var sequence = new[] { "a.png", "b.png" };

        var decision = PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: sequence,
            CurrentIndex: 0,
            CurrentPath: "a.png",
            Direction: 1,
            CurrentFileTypeHint: PhotoFileType.Pdf));

        decision.CurrentFileType.Should().Be(PhotoFileType.Pdf);
        decision.ShouldNavigateFile.Should().BeFalse();
    }
}

