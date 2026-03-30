using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBackgroundDuplicateWindowPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldReturnTrue_ForSameBackgroundBaseWithinWindow()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.NeighborRender));
        var previous = CrossPageUpdateRequestContextFactory.Create(
            CrossPageUpdateSources.NeighborRender);

        var shouldSkip = CrossPageBackgroundDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-8),
            duplicateWindowMs: 24);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForDifferentBackgroundBase()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborRender);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborMissing);

        var shouldSkip = CrossPageBackgroundDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-8),
            duplicateWindowMs: 24);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForInteractionSource()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        var shouldSkip = CrossPageBackgroundDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-5),
            duplicateWindowMs: 24);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldUseSourceAwareWindow_ForNeighborMissing()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborMissing);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborMissing);

        var shouldSkip = CrossPageBackgroundDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            now.AddMilliseconds(-30),
            duplicateWindowMs: 24);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenTimestampNotInitialized()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborRender);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborRender);

        var shouldSkip = CrossPageBackgroundDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            now,
            CrossPageRuntimeDefaults.UnsetTimestampUtc,
            duplicateWindowMs: 24);

        shouldSkip.Should().BeFalse();
    }
}
