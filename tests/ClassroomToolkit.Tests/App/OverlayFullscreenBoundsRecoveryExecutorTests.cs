using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayFullscreenBoundsRecoveryExecutorTests
{
    [Fact]
    public void Apply_ShouldNormalizeAndInvokeImmediateAndDeferred_WhenRequested()
    {
        var normalizeCalls = 0;
        var immediateCalls = 0;
        var deferredCalls = 0;

        OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: shouldNormalize =>
            {
                if (shouldNormalize)
                {
                    normalizeCalls++;
                }
            },
            applyImmediateBounds: () => immediateCalls++,
            applyDeferredBounds: () => deferredCalls++);

        normalizeCalls.Should().Be(1);
        immediateCalls.Should().Be(1);
        deferredCalls.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldDoNothing_WhenRecoveryNotRequested()
    {
        var normalizeCalls = 0;
        var immediateCalls = 0;
        var deferredCalls = 0;

        OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: false,
            normalizeWindowState: shouldNormalize =>
            {
                if (shouldNormalize)
                {
                    normalizeCalls++;
                }
            },
            applyImmediateBounds: () => immediateCalls++,
            applyDeferredBounds: () => deferredCalls++);

        normalizeCalls.Should().Be(0);
        immediateCalls.Should().Be(0);
        deferredCalls.Should().Be(0);
    }
}
