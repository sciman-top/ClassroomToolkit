using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDuplicateWindowPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnVisualSync_WhenVisualSyncDuplicate()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InkStateChanged));
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.InkStateChanged);

        var decision = CrossPageDuplicateWindowPolicy.Resolve(
            current,
            previous,
            now,
            now.AddMilliseconds(-5));

        decision.ShouldSkip.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.VisualSync);
    }

    [Fact]
    public void Resolve_ShouldReturnBackground_WhenBackgroundDuplicate()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.NeighborRender));
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.NeighborRender);

        var decision = CrossPageDuplicateWindowPolicy.Resolve(
            current,
            previous,
            now,
            now.AddMilliseconds(-8));

        decision.ShouldSkip.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.BackgroundRefresh);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenNotDuplicate()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.StepViewport);

        var decision = CrossPageDuplicateWindowPolicy.Resolve(
            current,
            previous,
            now,
            now.AddMilliseconds(-5));

        decision.ShouldSkip.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnInteraction_WhenInteractionDuplicate()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        var decision = CrossPageDuplicateWindowPolicy.Resolve(
            current,
            previous,
            now,
            now.AddMilliseconds(-4));

        decision.ShouldSkip.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.Interaction);
    }

    [Fact]
    public void Resolve_RuntimeStateOverload_ShouldUseStateSnapshot()
    {
        var now = DateTime.UtcNow;
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var state = new CrossPageUpdateRequestRuntimeState(
            LastRequest: CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan),
            LastRequestUtc: now.AddMilliseconds(-4));

        var decision = CrossPageDuplicateWindowPolicy.Resolve(current, state, now);

        decision.ShouldSkip.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDuplicateWindowSkipReason.Interaction);
    }
}
