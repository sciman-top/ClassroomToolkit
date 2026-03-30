using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStateNormalizationExecutorTests
{
    [Fact]
    public void Resolve_ShouldReturnNormalizationRequested_WhenRequested()
    {
        var decision = WindowStateNormalizationExecutor.Resolve(
            target: "window",
            shouldNormalize: true);

        decision.ShouldNormalize.Should().BeTrue();
        decision.Reason.Should().Be(WindowStateNormalizationReason.NormalizationRequested);
    }

    [Fact]
    public void Apply_ShouldSetWindowStateToNormal_WhenRequested()
    {
        var state = WindowState.Minimized;

        var applied = WindowStateNormalizationExecutor.Apply(
            target: "window",
            shouldNormalize: true,
            applyNormalize: (_, shouldNormalize) =>
            {
                if (shouldNormalize)
                {
                    state = WindowState.Normal;
                    return true;
                }

                return false;
            });

        applied.Should().BeTrue();
        state.Should().Be(WindowState.Normal);
    }

    [Fact]
    public void Resolve_ShouldReturnNormalizationNotRequested_WhenNormalizationNotRequested()
    {
        var decision = WindowStateNormalizationExecutor.Resolve(
            target: "window",
            shouldNormalize: false);

        decision.ShouldNormalize.Should().BeFalse();
        decision.Reason.Should().Be(WindowStateNormalizationReason.NormalizationNotRequested);
    }

    [Fact]
    public void Apply_ShouldNotChangeState_WhenNormalizationNotRequested()
    {
        var state = WindowState.Minimized;

        var applied = WindowStateNormalizationExecutor.Apply(
            target: "window",
            shouldNormalize: false,
            applyNormalize: (_, shouldNormalize) =>
            {
                if (shouldNormalize)
                {
                    state = WindowState.Normal;
                    return true;
                }

                return false;
            });

        applied.Should().BeFalse();
        state.Should().Be(WindowState.Minimized);
    }

    [Fact]
    public void Resolve_ShouldReturnTargetMissing_WhenTargetIsNull()
    {
        var decision = WindowStateNormalizationExecutor.Resolve<string>(
            target: null,
            shouldNormalize: true);

        decision.ShouldNormalize.Should().BeFalse();
        decision.Reason.Should().Be(WindowStateNormalizationReason.TargetMissing);
    }

    [Fact]
    public void Apply_ShouldReturnFalse_WhenTargetIsNull()
    {
        var applied = WindowStateNormalizationExecutor.Apply<string>(
            target: null,
            shouldNormalize: true,
            applyNormalize: (_, _) => true);

        applied.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenApplyNormalizeIsNull()
    {
        var act = () => WindowStateNormalizationExecutor.Apply(
            target: "window",
            shouldNormalize: true,
            applyNormalize: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldReturnFalse_WhenApplyNormalizeThrowsNonFatal()
    {
        var applied = WindowStateNormalizationExecutor.Apply(
            target: "window",
            shouldNormalize: true,
            applyNormalize: (_, _) => throw new InvalidOperationException("normalize-failed"));

        applied.Should().BeFalse();
    }
}
