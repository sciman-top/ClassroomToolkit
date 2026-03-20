using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPresentationDispatchCoordinatorTests
{
    [Fact]
    public void TryDispatch_ShouldReturnFalse_WhenAllChannelsDisabled()
    {
        var provider = new FakeSnapshotProvider();
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);

        var result = coordinator.TryDispatch(
            allowOffice: false,
            allowWps: false,
            currentPresentationType: PresentationType.None,
            trySendWps: (_, _) => true,
            trySendOffice: (_, _) => true);

        result.Should().BeFalse();
        provider.ResolveCallCount.Should().Be(0);
    }

    [Fact]
    public void TryDispatch_ShouldPreferForegroundWps()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new OverlayPresentationTargetSnapshot(
                WpsTarget: BuildTarget(1001, "wpspresentation.exe", "wpsshowframe"),
                OfficeTarget: BuildTarget(2002, "powerpnt.exe", "screenclass"),
                WpsSlideshow: true,
                OfficeSlideshow: true,
                WpsFullscreen: false,
                OfficeFullscreen: false,
                ForegroundType: PresentationType.Wps)
        };
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);
        var sentWps = new List<bool>();
        var sentOffice = new List<bool>();

        var result = coordinator.TryDispatch(
            allowOffice: true,
            allowWps: true,
            currentPresentationType: PresentationType.None,
            trySendWps: (_, allowBackground) =>
            {
                sentWps.Add(allowBackground);
                return true;
            },
            trySendOffice: (_, allowBackground) =>
            {
                sentOffice.Add(allowBackground);
                return true;
            });

        result.Should().BeTrue();
        sentWps.Should().Equal(false);
        sentOffice.Should().BeEmpty();
    }

    [Fact]
    public void TryDispatch_ShouldFallbackToOffice_WhenWpsFailsAndOfficeAvailable()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new OverlayPresentationTargetSnapshot(
                WpsTarget: BuildTarget(3003, "wpspresentation.exe", "wpsshowframe"),
                OfficeTarget: BuildTarget(4004, "powerpnt.exe", "screenclass"),
                WpsSlideshow: true,
                OfficeSlideshow: true,
                WpsFullscreen: false,
                OfficeFullscreen: true,
                ForegroundType: PresentationType.None)
        };
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);
        var sentOffice = new List<bool>();

        var result = coordinator.TryDispatch(
            allowOffice: true,
            allowWps: true,
            currentPresentationType: PresentationType.None,
            trySendWps: (_, _) => false,
            trySendOffice: (_, allowBackground) =>
            {
                sentOffice.Add(allowBackground);
                return true;
            });

        result.Should().BeTrue();
        sentOffice.Should().Equal(true);
    }

    [Fact]
    public void TryDispatch_ShouldReturnFalse_WhenSnapshotProviderThrowsNonFatal()
    {
        var provider = new ThrowingSnapshotProvider(new InvalidOperationException("non-fatal"));
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);

        var result = coordinator.TryDispatch(
            allowOffice: true,
            allowWps: true,
            currentPresentationType: PresentationType.None,
            trySendWps: (_, _) => true,
            trySendOffice: (_, _) => true);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryDispatch_ShouldRethrow_WhenSnapshotProviderThrowsFatal()
    {
        var provider = new ThrowingSnapshotProvider(new BadImageFormatException("fatal"));
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);

        var act = () => coordinator.TryDispatch(
            allowOffice: true,
            allowWps: true,
            currentPresentationType: PresentationType.None,
            trySendWps: (_, _) => true,
            trySendOffice: (_, _) => true);

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void TryDispatch_ShouldReturnFalse_WhenNoSlideshowDetected()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new OverlayPresentationTargetSnapshot(
                WpsTarget: BuildTarget(7777, "wpspresentation.exe", "randomclass"),
                OfficeTarget: BuildTarget(8888, "powerpnt.exe", "randomclass"),
                WpsSlideshow: false,
                OfficeSlideshow: false,
                WpsFullscreen: true,
                OfficeFullscreen: true,
                ForegroundType: PresentationType.Wps)
        };
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);
        var wpsCallCount = 0;
        var officeCallCount = 0;

        var result = coordinator.TryDispatch(
            allowOffice: true,
            allowWps: true,
            currentPresentationType: PresentationType.Wps,
            trySendWps: (_, _) =>
            {
                wpsCallCount++;
                return true;
            },
            trySendOffice: (_, _) =>
            {
                officeCallCount++;
                return true;
            });

        result.Should().BeFalse();
        wpsCallCount.Should().Be(0);
        officeCallCount.Should().Be(0);
    }

    [Fact]
    public void TryDispatch_ShouldNotInvokeSenders_WhenTargetsAreInvalid()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new OverlayPresentationTargetSnapshot(
                WpsTarget: PresentationTarget.Empty,
                OfficeTarget: PresentationTarget.Empty,
                WpsSlideshow: true,
                OfficeSlideshow: true,
                WpsFullscreen: true,
                OfficeFullscreen: false,
                ForegroundType: PresentationType.Wps)
        };
        var coordinator = new OverlayPresentationDispatchCoordinator(provider);
        var wpsCallCount = 0;
        var officeCallCount = 0;

        var result = coordinator.TryDispatch(
            allowOffice: true,
            allowWps: true,
            currentPresentationType: PresentationType.Wps,
            trySendWps: (_, _) =>
            {
                wpsCallCount++;
                return true;
            },
            trySendOffice: (_, _) =>
            {
                officeCallCount++;
                return true;
            });

        result.Should().BeFalse();
        wpsCallCount.Should().Be(0);
        officeCallCount.Should().Be(0);
    }

    private static PresentationTarget BuildTarget(long hwnd, string process, params string[] classNames)
    {
        return new PresentationTarget(
            new IntPtr(hwnd),
            new PresentationWindowInfo(1, process, classNames));
    }

    private sealed class FakeSnapshotProvider : IOverlayPresentationTargetSnapshotProvider
    {
        public OverlayPresentationTargetSnapshot Snapshot { get; set; }
        public int ResolveCallCount { get; private set; }

        public OverlayPresentationTargetSnapshot Resolve(bool allowWps, bool allowOffice)
        {
            ResolveCallCount++;
            return Snapshot;
        }
    }

    private sealed class ThrowingSnapshotProvider : IOverlayPresentationTargetSnapshotProvider
    {
        private readonly Exception _exception;

        public ThrowingSnapshotProvider(Exception exception)
        {
            _exception = exception;
        }

        public OverlayPresentationTargetSnapshot Resolve(bool allowWps, bool allowOffice)
        {
            throw _exception;
        }
    }
}
