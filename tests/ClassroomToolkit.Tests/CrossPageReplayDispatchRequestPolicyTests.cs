using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayDispatchRequestPolicyTests
{
    [Fact]
    public void ResolveSource_ShouldMapKnownTargets()
    {
        CrossPageReplayDispatchRequestPolicy.ResolveSource(CrossPageReplayDispatchTarget.VisualSync)
            .Should().Be(CrossPageUpdateSources.InkVisualSyncReplay);
        CrossPageReplayDispatchRequestPolicy.ResolveSource(CrossPageReplayDispatchTarget.Interaction)
            .Should().Be(CrossPageUpdateSources.InteractionReplay);
    }

    [Fact]
    public void ResolveSource_ShouldReturnNull_ForNone()
    {
        CrossPageReplayDispatchRequestPolicy.ResolveSource(CrossPageReplayDispatchTarget.None)
            .Should().BeNull();
    }
}
