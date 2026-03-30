using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractionDuplicateWindowPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldReturnTrue_ForSameInteractionBaseWithinWindow()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        var shouldSkip = CrossPageInteractionDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-4),
            duplicateWindowMs: 8);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForDifferentInteractionBase()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.StepViewport);

        var shouldSkip = CrossPageInteractionDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-4),
            duplicateWindowMs: 8);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForNonInteractionKind()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborRender);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborRender);

        var shouldSkip = CrossPageInteractionDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-4),
            duplicateWindowMs: 8);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldUseSourceAwareWindow_ForPhotoPan()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        var shouldSkip = CrossPageInteractionDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-10),
            duplicateWindowMs: 8);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenTimestampNotInitialized()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        var shouldSkip = CrossPageInteractionDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            CrossPageRuntimeDefaults.UnsetTimestampUtc,
            duplicateWindowMs: 8);

        shouldSkip.Should().BeFalse();
    }
}
