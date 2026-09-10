using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Tests;

public sealed class PresentationTargetSessionBindingTests
{
    [Fact]
    public void Resolve_ShouldReuseAdmittedTargetUntilInvalidated()
    {
        var binding = new PresentationTargetSessionBinding();
        var first = BuildTarget(100);
        var second = BuildTarget(200);
        var resolveCount = 0;

        var resolvedFirst = binding.Resolve(
            PresentationType.Wps,
            () =>
            {
                resolveCount++;
                return resolveCount == 1 ? first : second;
            },
            target => target.IsValid);
        var resolvedSecond = binding.Resolve(
            PresentationType.Wps,
            () =>
            {
                resolveCount++;
                return second;
            },
            target => target.IsValid);

        resolvedFirst.Should().Be(first);
        resolvedSecond.Should().Be(first);
        resolveCount.Should().Be(1);

        binding.Invalidate(PresentationType.Wps);
        binding.Resolve(
            PresentationType.Wps,
            () =>
            {
                resolveCount++;
                return second;
            },
            target => target.IsValid).Should().Be(second);
        resolveCount.Should().Be(2);
    }

    [Fact]
    public void Resolve_ShouldClearBindingWhenAdmissionFails()
    {
        var binding = new PresentationTargetSessionBinding();
        var target = BuildTarget(300);
        var admitted = true;

        binding.Resolve(PresentationType.Office, () => target, _ => admitted)
            .Should().Be(target);

        admitted = false;
        binding.Resolve(PresentationType.Office, () => target, _ => admitted)
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidateIfBoundToDifferentWindow_ShouldForceNewSessionTarget()
    {
        var binding = new PresentationTargetSessionBinding();
        var first = BuildTarget(400);
        var second = BuildTarget(500);
        var resolveCount = 0;

        binding.Resolve(
                PresentationType.Wps,
                () =>
                {
                    resolveCount++;
                    return resolveCount == 1 ? first : second;
                },
                target => target.IsValid)
            .Should().Be(first);

        binding.InvalidateIfBoundToDifferentWindow(PresentationType.Wps, second.Handle)
            .Should().BeTrue();

        binding.Resolve(
                PresentationType.Wps,
                () =>
                {
                    resolveCount++;
                    return second;
                },
                target => target.IsValid)
            .Should().Be(second);
        resolveCount.Should().Be(2);
    }

    [Fact]
    public void InvalidateIfBoundToDifferentWindow_ShouldKeepSameSessionTarget()
    {
        var binding = new PresentationTargetSessionBinding();
        var target = BuildTarget(600);

        binding.Resolve(PresentationType.Office, () => target, candidate => candidate.IsValid)
            .Should().Be(target);

        binding.InvalidateIfBoundToDifferentWindow(PresentationType.Office, target.Handle)
            .Should().BeFalse();
        binding.InvalidateIfBoundToDifferentWindow(PresentationType.Office, IntPtr.Zero)
            .Should().BeFalse();

        binding.Resolve(PresentationType.Office, () => BuildTarget(700), candidate => candidate.IsValid)
            .Should().Be(target);
    }

    [Fact]
    public void Resolve_ShouldPreferAdmittedForegroundTarget_WhenSessionBindingIsStale()
    {
        var binding = new PresentationTargetSessionBinding();
        var oldTarget = BuildTarget(800);
        var foregroundTarget = BuildTarget(900);
        var fallbackCalled = false;

        binding.Resolve(PresentationType.Wps, () => oldTarget, candidate => candidate.IsValid)
            .Should().Be(oldTarget);

        binding.Resolve(
                PresentationType.Wps,
                () =>
                {
                    fallbackCalled = true;
                    return oldTarget;
                },
                candidate => candidate.IsValid,
                preferredTarget: foregroundTarget)
            .Should().Be(foregroundTarget);

        fallbackCalled.Should().BeFalse();
    }

    private static PresentationTarget BuildTarget(long hwnd)
    {
        return new PresentationTarget(
            new IntPtr(hwnd),
            new PresentationWindowInfo(1, "wpspresentation.exe", ["wpsshowframe"]));
    }
}
