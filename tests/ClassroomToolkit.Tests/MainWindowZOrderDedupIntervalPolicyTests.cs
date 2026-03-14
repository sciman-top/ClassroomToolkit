using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowZOrderDedupIntervalPolicyTests
{
    [Fact]
    public void ResolveIntervals_ShouldDelegateByInteractionState()
    {
        var interactionState = new MainWindowOverlayInteractionState(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false);

        var surfaceInterval = MainWindowZOrderDedupIntervalPolicy.ResolveSurfaceDecisionIntervalMs(interactionState);
        var requestInterval = MainWindowZOrderDedupIntervalPolicy.ResolveRequestIntervalMs(interactionState);

        surfaceInterval.Should().BePositive();
        requestInterval.Should().BePositive();
    }
}
