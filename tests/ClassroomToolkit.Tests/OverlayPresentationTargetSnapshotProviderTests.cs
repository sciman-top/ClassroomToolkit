using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPresentationTargetSnapshotProviderTests
{
    [Fact]
    public void Resolve_ShouldReturnEmptyTargets_WhenChannelsDisabled()
    {
        var resolver = new CountingResolver();
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            _ => false,
            currentProcessId: 100);

        var snapshot = provider.Resolve(allowWps: false, allowOffice: false);

        snapshot.WpsTarget.IsValid.Should().BeFalse();
        snapshot.OfficeTarget.IsValid.Should().BeFalse();
        snapshot.WpsSlideshow.Should().BeFalse();
        snapshot.OfficeSlideshow.Should().BeFalse();
        resolver.ResolvePresentationTargetCallCount.Should().Be(0);
        resolver.ResolveForegroundCallCount.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldClassifyForegroundAndSlideshows()
    {
        var wpsTarget = BuildTarget(1100, 11, "wpspresentation.exe", "wpsshowframeclass");
        var officeTarget = BuildTarget(2200, 22, "powerpnt.exe", "screenclass");
        var foregroundTarget = BuildTarget(2200, 22, "powerpnt.exe", "screenclass");
        var resolver = new FakeResolver
        {
            WpsTarget = wpsTarget,
            OfficeTarget = officeTarget,
            ForegroundTarget = foregroundTarget
        };
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            hwnd => hwnd == new IntPtr(1100) || hwnd == new IntPtr(2200),
            currentProcessId: 100);

        var snapshot = provider.Resolve(allowWps: true, allowOffice: true);

        snapshot.WpsTarget.Should().Be(wpsTarget);
        snapshot.OfficeTarget.Should().Be(officeTarget);
        snapshot.WpsSlideshow.Should().BeTrue();
        snapshot.OfficeSlideshow.Should().BeTrue();
        snapshot.ForegroundType.Should().Be(PresentationType.Office);
        snapshot.WpsFullscreen.Should().BeTrue();
        snapshot.OfficeFullscreen.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldUseFullscreenAsSlideshowFallback()
    {
        var wpsTarget = BuildTarget(3300, 33, "wpspresentation.exe", "randomclass");
        var resolver = new FakeResolver
        {
            WpsTarget = wpsTarget,
            ForegroundTarget = PresentationTarget.Empty
        };
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            hwnd => hwnd == new IntPtr(3300),
            currentProcessId: 100);

        var snapshot = provider.Resolve(allowWps: true, allowOffice: false);

        snapshot.WpsSlideshow.Should().BeTrue();
        snapshot.ForegroundType.Should().Be(PresentationType.None);
    }

    [Fact]
    public void Resolve_ShouldFallbackToDefaultClassifier_WhenAccessorReturnsNull()
    {
        var officeTarget = BuildTarget(6600, 66, "powerpnt.exe", "screenclass");
        var resolver = new FakeResolver
        {
            OfficeTarget = officeTarget,
            ForegroundTarget = officeTarget
        };
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => null!,
            _ => false,
            currentProcessId: 100);

        var snapshot = provider.Resolve(allowWps: false, allowOffice: true);

        snapshot.OfficeTarget.IsValid.Should().BeTrue();
        snapshot.ForegroundType.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void Resolve_ShouldEvaluateFullscreenAtMostOncePerTarget()
    {
        var wpsTarget = BuildTarget(4400, 44, "wpspresentation.exe", "randomclass");
        var officeTarget = BuildTarget(5500, 55, "powerpnt.exe", "randomclass");
        var resolver = new FakeResolver
        {
            WpsTarget = wpsTarget,
            OfficeTarget = officeTarget
        };
        var fullscreenCallCountByHandle = new Dictionary<long, int>();
        bool IsFullscreen(IntPtr hwnd)
        {
            var key = hwnd.ToInt64();
            if (!fullscreenCallCountByHandle.TryAdd(key, 1))
            {
                fullscreenCallCountByHandle[key]++;
            }

            return true;
        }

        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            IsFullscreen,
            currentProcessId: 100);

        _ = provider.Resolve(allowWps: true, allowOffice: true);

        fullscreenCallCountByHandle[4400].Should().Be(1);
        fullscreenCallCountByHandle[5500].Should().Be(1);
    }

    [Fact]
    public void Resolve_ShouldReturnEmptySnapshot_WhenResolverThrowsNonFatal()
    {
        var resolver = new ThrowingResolver(new InvalidOperationException("non-fatal"));
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            _ => false,
            currentProcessId: 100);

        var snapshot = provider.Resolve(allowWps: true, allowOffice: true);

        snapshot.WpsTarget.IsValid.Should().BeFalse();
        snapshot.OfficeTarget.IsValid.Should().BeFalse();
        snapshot.ForegroundType.Should().Be(PresentationType.None);
    }

    [Fact]
    public void Resolve_ShouldRethrow_WhenResolverThrowsFatal()
    {
        var resolver = new ThrowingResolver(new BadImageFormatException("fatal"));
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            _ => false,
            currentProcessId: 100);

        var act = () => provider.Resolve(allowWps: true, allowOffice: true);

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void Resolve_ShouldNormalizeCurrentProcessId_WhenConstructorReceivesZero()
    {
        var resolver = new CapturingResolver();
        var provider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            () => new PresentationClassifier(),
            _ => false,
            currentProcessId: 0);

        _ = provider.Resolve(allowWps: true, allowOffice: false);

        resolver.LastExcludeProcessId.Should().NotBeNull();
        resolver.LastExcludeProcessId.Should().NotBe(0u);
    }

    private static PresentationTarget BuildTarget(long hwnd, uint processId, string processName, params string[] classNames)
    {
        return new PresentationTarget(
            new IntPtr(hwnd),
            new PresentationWindowInfo(processId, processName, classNames));
    }

    private sealed class FakeResolver : IPresentationTargetResolver
    {
        public PresentationTarget ForegroundTarget { get; set; } = PresentationTarget.Empty;
        public PresentationTarget WpsTarget { get; set; } = PresentationTarget.Empty;
        public PresentationTarget OfficeTarget { get; set; } = PresentationTarget.Empty;

        public PresentationTarget ResolveForeground()
        {
            return ForegroundTarget;
        }

        public PresentationTarget ResolvePresentationTarget(
            PresentationClassifier classifier,
            bool allowWps,
            bool allowOffice,
            uint? excludeProcessId = null)
        {
            if (allowWps && !allowOffice)
            {
                return WpsTarget;
            }
            if (allowOffice && !allowWps)
            {
                return OfficeTarget;
            }

            return PresentationTarget.Empty;
        }
    }

    private sealed class ThrowingResolver : IPresentationTargetResolver
    {
        private readonly Exception _exception;

        public ThrowingResolver(Exception exception)
        {
            _exception = exception;
        }

        public PresentationTarget ResolveForeground()
        {
            throw _exception;
        }

        public PresentationTarget ResolvePresentationTarget(
            PresentationClassifier classifier,
            bool allowWps,
            bool allowOffice,
            uint? excludeProcessId = null)
        {
            throw _exception;
        }
    }

    private sealed class CapturingResolver : IPresentationTargetResolver
    {
        public uint? LastExcludeProcessId { get; private set; }

        public PresentationTarget ResolveForeground()
        {
            return PresentationTarget.Empty;
        }

        public PresentationTarget ResolvePresentationTarget(
            PresentationClassifier classifier,
            bool allowWps,
            bool allowOffice,
            uint? excludeProcessId = null)
        {
            LastExcludeProcessId = excludeProcessId;
            return PresentationTarget.Empty;
        }
    }

    private sealed class CountingResolver : IPresentationTargetResolver
    {
        public int ResolveForegroundCallCount { get; private set; }
        public int ResolvePresentationTargetCallCount { get; private set; }

        public PresentationTarget ResolveForeground()
        {
            ResolveForegroundCallCount++;
            return PresentationTarget.Empty;
        }

        public PresentationTarget ResolvePresentationTarget(
            PresentationClassifier classifier,
            bool allowWps,
            bool allowOffice,
            uint? excludeProcessId = null)
        {
            ResolvePresentationTargetCallCount++;
            return PresentationTarget.Empty;
        }
    }
}
