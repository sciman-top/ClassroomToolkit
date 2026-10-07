using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class CrossPageDuplicateWindowPolicyTests
{
    private static readonly DateTime NowUtc = new(2026, 3, 7, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_ShouldReturnReasonForEachDuplicateKind()
    {
        Resolve(CrossPageUpdateSources.InkStateChanged, CrossPageUpdateSources.InkStateChanged, 5)
            .Reason.Should().Be(CrossPageDuplicateWindowSkipReason.VisualSync);
        Resolve(CrossPageUpdateSources.NeighborRender, CrossPageUpdateSources.NeighborRender, 5)
            .Reason.Should().Be(CrossPageDuplicateWindowSkipReason.BackgroundRefresh);
        Resolve(CrossPageUpdateSources.PhotoPan, CrossPageUpdateSources.PhotoPan, 5)
            .Reason.Should().Be(CrossPageDuplicateWindowSkipReason.Interaction);
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.UndoSnapshot, 24)]
    [InlineData(CrossPageUpdateSources.RegionEraseCrossPage, 20)]
    [InlineData(CrossPageUpdateSources.InkRedrawCompleted, 18)]
    [InlineData(CrossPageUpdateSources.InkStateChanged, 14)]
    [InlineData(CrossPageUpdateSources.InkShowEnabled, 14)]
    public void Resolve_ShouldUseSourceAwareVisualSyncWindow(string source, int windowMs)
    {
        var withinWindow = Resolve(source, source, windowMs - 1);
        var atWindowBoundary = Resolve(source, source, windowMs);

        withinWindow.ShouldSkip.Should().BeTrue();
        withinWindow.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.VisualSync);
        atWindowBoundary.ShouldSkip.Should().BeFalse();
        atWindowBoundary.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.None);
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.NeighborMissing, 36)]
    [InlineData(CrossPageUpdateSources.NeighborMissingDelayed, 36)]
    [InlineData(CrossPageUpdateSources.NeighborSidecar, 32)]
    [InlineData(CrossPageUpdateSources.NeighborRender, 28)]
    [InlineData(CrossPageUpdateSources.NeighborPrefix + "other", 24)]
    public void Resolve_ShouldUseSourceAwareBackgroundWindow(string source, int windowMs)
    {
        var withinWindow = Resolve(source, source, windowMs - 1);
        var atWindowBoundary = Resolve(source, source, windowMs);

        withinWindow.ShouldSkip.Should().BeTrue();
        withinWindow.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.BackgroundRefresh);
        atWindowBoundary.ShouldSkip.Should().BeFalse();
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.PhotoPan, 24)]
    [InlineData(CrossPageUpdateSources.ManipulationDelta, 24)]
    [InlineData(CrossPageUpdateSources.StepViewport, 24)]
    [InlineData(CrossPageUpdateSources.ApplyScale, 24)]
    [InlineData(CrossPageUpdateSources.PointerUpFast, 18)]
    [InlineData(CrossPageUpdateSources.PostInput, 8)]
    public void Resolve_ShouldUseSourceAwareInteractionWindow(string source, int windowMs)
    {
        var withinWindow = Resolve(source, source, windowMs - 1);
        var atWindowBoundary = Resolve(source, source, windowMs);

        withinWindow.ShouldSkip.Should().BeTrue();
        withinWindow.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.Interaction);
        atWindowBoundary.ShouldSkip.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldUseVisualSyncDefaultWindowForOtherVisualSources()
    {
        var current = CreateVisualRequest("custom-visual-source");
        var previous = current;

        Resolve(current, previous, NowUtc.AddMilliseconds(-11)).ShouldSkip.Should().BeTrue();
        Resolve(current, previous, NowUtc.AddMilliseconds(-12)).ShouldSkip.Should().BeFalse();
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.InkStateChanged, CrossPageUpdateSources.NeighborRender)]
    [InlineData(CrossPageUpdateSources.NeighborRender, CrossPageUpdateSources.PhotoPan)]
    [InlineData(CrossPageUpdateSources.PhotoPan, CrossPageUpdateSources.InkStateChanged)]
    [InlineData(CrossPageUpdateSources.NeighborMissing, CrossPageUpdateSources.NeighborRender)]
    [InlineData(CrossPageUpdateSources.InkStateChanged, CrossPageUpdateSources.InkRedrawCompleted)]
    [InlineData(CrossPageUpdateSources.PhotoPan, CrossPageUpdateSources.StepViewport)]
    public void Resolve_ShouldNotSkipDifferentKindOrBaseSource(string currentSource, string previousSource)
    {
        Resolve(currentSource, previousSource, 5).ShouldSkip.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCompareDelayedRequestByItsBaseSource()
    {
        var decision = Resolve(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InkStateChanged),
            CrossPageUpdateSources.InkStateChanged,
            5);

        decision.ShouldSkip.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.VisualSync);
    }

    [Fact]
    public void Resolve_ShouldNotSkipWhenPreviousRequestOrTimestampIsMissing()
    {
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        Resolve(current, null, NowUtc.AddMilliseconds(-5)).ShouldSkip.Should().BeFalse();
        Resolve(
            current,
            previous,
            CrossPageRuntimeDefaults.UnsetTimestampUtc).ShouldSkip.Should().BeFalse();
    }

    [Fact]
    public void Resolve_StateOverloadShouldUseRequestSnapshot()
    {
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var state = new CrossPageUpdateRequestRuntimeState(
            CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan),
            NowUtc.AddMilliseconds(-4));

        var decision = CrossPageDuplicateWindowPolicy.Resolve(current, state, NowUtc);

        decision.ShouldSkip.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.Interaction);
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.InteractionReplay, CrossPageUpdateSources.InteractionReplay)]
    [InlineData(CrossPageUpdateSources.InkVisualSyncReplay, CrossPageUpdateSources.InkVisualSyncReplay)]
    [InlineData(CrossPageUpdateSources.InteractionReplay, CrossPageUpdateSources.InkVisualSyncReplay)]
    public void Resolve_ShouldNotSuppressReplayRequestsInVisualSyncLane(string currentSource, string previousSource)
    {
        var current = CreateVisualRequest(currentSource);
        var previous = CreateVisualRequest(previousSource);

        var decision = Resolve(current, previous, NowUtc.AddMilliseconds(-5));

        decision.ShouldSkip.Should().BeFalse();
    }

    private static CrossPageDuplicateWindowDecision Resolve(
        string currentSource,
        string? previousSource,
        int elapsedMs)
    {
        var current = CrossPageUpdateRequestContextFactory.Create(currentSource);
        var previous = previousSource == null
            ? (CrossPageUpdateRequestContext?)null
            : CrossPageUpdateRequestContextFactory.Create(previousSource);
        return Resolve(current, previous, NowUtc.AddMilliseconds(-elapsedMs));
    }

    private static CrossPageDuplicateWindowDecision Resolve(
        CrossPageUpdateRequestContext current,
        CrossPageUpdateRequestContext? previous,
        DateTime lastRequestedUtc)
    {
        return CrossPageDuplicateWindowPolicy.Resolve(current, previous, NowUtc, lastRequestedUtc);
    }

    private static CrossPageUpdateRequestContext CreateVisualRequest(string source)
    {
        return new CrossPageUpdateRequestContext(
            source,
            source,
            CrossPageUpdateSourceKind.VisualSync);
    }
}
