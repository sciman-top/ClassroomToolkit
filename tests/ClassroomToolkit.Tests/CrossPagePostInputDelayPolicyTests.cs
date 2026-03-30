using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePostInputDelayPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldUseConfiguredDelay_ForPostInput()
    {
        var value = CrossPagePostInputDelayPolicy.ResolveMs(
            CrossPageUpdateSources.PostInput,
            configuredDelayMs: 120);

        value.Should().Be(120);
    }

    [Fact]
    public void ResolveMs_ShouldRaiseDelay_ForNeighborRender()
    {
        var value = CrossPagePostInputDelayPolicy.ResolveMs(
            CrossPageUpdateSources.NeighborRender,
            configuredDelayMs: 120);

        value.Should().Be(CrossPagePostInputDelayThresholds.NeighborRenderMinMs);
    }

    [Fact]
    public void ResolveMs_ShouldRaiseDelay_ForReplaySource()
    {
        var value = CrossPagePostInputDelayPolicy.ResolveMs(
            CrossPageUpdateSources.InkVisualSyncReplay,
            configuredDelayMs: 120);

        value.Should().Be(CrossPagePostInputDelayThresholds.ReplayMinMs);
    }

    [Fact]
    public void ResolveMs_ShouldHonorOverrideBeforeMinimumRules()
    {
        var value = CrossPagePostInputDelayPolicy.ResolveMs(
            CrossPageUpdateSources.PostInput,
            configuredDelayMs: 120,
            fallbackDelayMs: CrossPagePostInputDelayThresholds.FallbackDelayMs,
            delayOverrideMs: 260);

        value.Should().Be(260);
    }
}
