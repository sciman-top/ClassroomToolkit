using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageVisualSyncDuplicateWindowPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldUseRequestContextOverload()
    {
        var nowUtc = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InkStateChanged));
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.InkStateChanged);

        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            current,
            previous,
            nowUtc,
            nowUtc.AddMilliseconds(-6),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnTrue_ForSameVisualSyncBaseWithinWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InkStateChanged),
            CrossPageUpdateSources.InkStateChanged,
            nowUtc,
            nowUtc.AddMilliseconds(-6),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForDifferentVisualSyncBase()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.InkRedrawCompleted,
            CrossPageUpdateSources.InkStateChanged,
            nowUtc,
            nowUtc.AddMilliseconds(-6),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForInteractionSource()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.PhotoPan,
            CrossPageUpdateSources.PhotoPan,
            nowUtc,
            nowUtc.AddMilliseconds(-3),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForSameInteractionReplayBaseWithinWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InteractionReplay),
            CrossPageUpdateSources.InteractionReplay,
            nowUtc,
            nowUtc.AddMilliseconds(-5),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForSameInkVisualSyncReplayBaseWithinWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InkVisualSyncReplay),
            CrossPageUpdateSources.InkVisualSyncReplay,
            nowUtc,
            nowUtc.AddMilliseconds(-5),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_ForDifferentReplayBase()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.InteractionReplay,
            CrossPageUpdateSources.InkVisualSyncReplay,
            nowUtc,
            nowUtc.AddMilliseconds(-5),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkip_ShouldUseSourceAwareWindow_ForUndoSnapshot()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.UndoSnapshot,
            CrossPageUpdateSources.UndoSnapshot,
            nowUtc,
            nowUtc.AddMilliseconds(-18),
            duplicateWindowMs: 12);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenTimestampNotInitialized()
    {
        var nowUtc = DateTime.UtcNow;
        var shouldSkip = CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
            CrossPageUpdateSources.InkStateChanged,
            CrossPageUpdateSources.InkStateChanged,
            nowUtc,
            CrossPageRuntimeDefaults.UnsetTimestampUtc,
            duplicateWindowMs: 12);

        shouldSkip.Should().BeFalse();
    }
}
