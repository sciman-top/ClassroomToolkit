using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerStateChangeTransitionCoordinatorTests
{
    [Fact]
    public void Apply_ShouldDoNothing_WhenOverlayNormalizationIsNotRequested()
    {
        var normalizeCalls = 0;
        var surfaceCalls = 0;

        var result = ImageManagerStateChangeTransitionCoordinator.Apply(
            new ImageManagerStateChangeDecision(
                NormalizeOverlayWindowState: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true),
            () => normalizeCalls++,
            _ => true,
            _ => surfaceCalls++);

        result.NormalizationExecution.Should().Be(ImageManagerStateChangeNormalizationExecutionKind.None);
        result.AppliedSurfaceDecision.Should().BeFalse();
        normalizeCalls.Should().Be(0);
        surfaceCalls.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldScheduleNormalization_AndApplySurfaceDecision_WhenSchedulerSucceeds()
    {
        var normalizeCalls = 0;
        var surfaceCalls = 0;

        var result = ImageManagerStateChangeTransitionCoordinator.Apply(
            new ImageManagerStateChangeDecision(
                NormalizeOverlayWindowState: true,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false),
            () => normalizeCalls++,
            action =>
            {
                action();
                return true;
            },
            _ => surfaceCalls++);

        result.NormalizationExecution.Should().Be(ImageManagerStateChangeNormalizationExecutionKind.Scheduled);
        result.AppliedSurfaceDecision.Should().BeTrue();
        normalizeCalls.Should().Be(1);
        surfaceCalls.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldFallbackToImmediateNormalization_WhenSchedulerFails()
    {
        var normalizeCalls = 0;
        var surfaceCalls = 0;

        var result = ImageManagerStateChangeTransitionCoordinator.Apply(
            new ImageManagerStateChangeDecision(
                NormalizeOverlayWindowState: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false),
            () => normalizeCalls++,
            _ => false,
            _ => surfaceCalls++);

        result.NormalizationExecution.Should().Be(ImageManagerStateChangeNormalizationExecutionKind.ImmediateFallback);
        result.AppliedSurfaceDecision.Should().BeFalse();
        normalizeCalls.Should().Be(1);
        surfaceCalls.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldFallbackWithoutThrow_WhenSchedulerThrowsNonFatal()
    {
        var normalizeCalls = 0;

        Action act = () =>
        {
            var result = ImageManagerStateChangeTransitionCoordinator.Apply(
                new ImageManagerStateChangeDecision(
                    NormalizeOverlayWindowState: true,
                    RequestZOrderApply: false,
                    ForceEnforceZOrder: false),
                () => normalizeCalls++,
                _ => throw new InvalidOperationException("schedule-failed"),
                _ => { });

            result.NormalizationExecution.Should().Be(ImageManagerStateChangeNormalizationExecutionKind.ImmediateFallback);
        };

        act.Should().NotThrow();
        normalizeCalls.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldSkipSurfaceDecision_WhenSurfaceDelegateThrowsNonFatal()
    {
        var result = ImageManagerStateChangeTransitionCoordinator.Apply(
            new ImageManagerStateChangeDecision(
                NormalizeOverlayWindowState: true,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false),
            () => { },
            _ => true,
            _ => throw new InvalidOperationException("surface-failed"));

        result.AppliedSurfaceDecision.Should().BeFalse();
    }
}
