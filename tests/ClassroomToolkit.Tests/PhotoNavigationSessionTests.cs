using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationSessionTests
{
    [Fact]
    public void Reset_ShouldExposeCurrentPath()
    {
        var session = new PhotoNavigationSession();

        session.Reset(new[] { "a.png", "b.png" }, 1);

        session.CurrentIndex.Should().Be(1);
        session.GetCurrentPath().Should().Be("b.png");
    }

    [Fact]
    public void Plan_ShouldUseOverlayPathToResolveStaleIndex()
    {
        var session = new PhotoNavigationSession();
        session.Reset(new[] { "a.png", "b.png", "c.png" }, 0);

        var decision = session.Plan("c.png", -1);
        session.SyncResolvedIndex(decision);

        session.CurrentIndex.Should().Be(2);
        decision.ShouldNavigateFile.Should().BeTrue();
        decision.NextIndex.Should().Be(1);
    }

    [Fact]
    public void TryApplyFileNavigation_ShouldUpdateIndexAndReturnPath()
    {
        var session = new PhotoNavigationSession();
        session.Reset(new[] { "a.png", "b.png" }, 0);

        var decision = new PhotoNavigationDecision(
            ShouldNavigateFile: true,
            ResolvedCurrentIndex: 0,
            NextIndex: 1,
            CurrentPath: "a.png",
            CurrentFileType: PhotoFileType.Image);

        var applied = session.TryApplyFileNavigation(decision, out var path);

        applied.Should().BeTrue();
        session.CurrentIndex.Should().Be(1);
        path.Should().Be("b.png");
    }

    [Fact]
    public void Plan_ShouldPassFileTypeHintToPlanner()
    {
        var session = new PhotoNavigationSession();
        session.Reset(new[] { "a.png", "b.png" }, 0);

        var decision = session.Plan("a.png", 1, PhotoFileType.Pdf);

        decision.CurrentFileType.Should().Be(PhotoFileType.Pdf);
        decision.ShouldNavigateFile.Should().BeFalse();
    }
}
