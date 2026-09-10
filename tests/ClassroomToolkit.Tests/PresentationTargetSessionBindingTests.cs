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

    private static PresentationTarget BuildTarget(long hwnd)
    {
        return new PresentationTarget(
            new IntPtr(hwnd),
            new PresentationWindowInfo(1, "wpspresentation.exe", ["wpsshowframe"]));
    }
}
