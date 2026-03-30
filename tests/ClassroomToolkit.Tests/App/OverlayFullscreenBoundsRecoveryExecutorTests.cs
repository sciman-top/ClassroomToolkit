using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayFullscreenBoundsRecoveryExecutorTests
{
    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenNormalizeDelegateIsNull()
    {
        Action act = () => OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: null!,
            applyImmediateBounds: () => { },
            applyDeferredBounds: () => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenImmediateDelegateIsNull()
    {
        Action act = () => OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: _ => { },
            applyImmediateBounds: null!,
            applyDeferredBounds: () => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenDeferredDelegateIsNull()
    {
        Action act = () => OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: _ => { },
            applyImmediateBounds: () => { },
            applyDeferredBounds: null!);

        act.Should().Throw<ArgumentNullException>();
    }

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

    [Fact]
    public void Apply_ShouldContinueWithoutThrow_WhenAnyDelegateThrowsNonFatal()
    {
        var immediateCalls = 0;
        var deferredCalls = 0;

        Action act = () => OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: _ => throw new InvalidOperationException("normalize-failed"),
            applyImmediateBounds: () =>
            {
                immediateCalls++;
                throw new InvalidOperationException("immediate-failed");
            },
            applyDeferredBounds: () => deferredCalls++);

        act.Should().NotThrow();
        immediateCalls.Should().Be(1);
        deferredCalls.Should().Be(1);
    }
}
