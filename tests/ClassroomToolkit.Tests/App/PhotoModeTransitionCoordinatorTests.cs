using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoModeTransitionCoordinatorTests
{
    [Fact]
    public void Apply_ShouldShowToolbarAndApplySurface_WhenPhotoModeBecomesActive()
    {
        var suppressValue = false;
        var normalizeCount = 0;
        var showCount = 0;
        var syncCount = 0;
        var surfaceCount = 0;
        var plan = PaintVisibilityTransitionPolicy.ResolvePhotoModeChange(
            photoModeActive: true,
            toolbarWindowState: System.Windows.WindowState.Minimized);

        var result = PhotoModeTransitionCoordinator.Apply(
            active: true,
            plan,
            value => suppressValue = value,
            () => normalizeCount++,
            () => showCount++,
            () => syncCount++,
            () => surfaceCount++);

        suppressValue.Should().BeTrue();
        result.NormalizedToolbarWindowState.Should().BeTrue();
        result.ShowedToolbarWindow.Should().BeTrue();
        result.SyncedOwners.Should().BeFalse();
        result.AppliedSurfaceDecision.Should().BeTrue();
        normalizeCount.Should().Be(1);
        showCount.Should().Be(1);
        syncCount.Should().Be(0);
        surfaceCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldSyncOwners_WhenPhotoModeBecomesInactive()
    {
        var syncCount = 0;
        var plan = PaintVisibilityTransitionPolicy.ResolvePhotoModeChange(
            photoModeActive: false,
            toolbarWindowState: System.Windows.WindowState.Normal);

        var result = PhotoModeTransitionCoordinator.Apply(
            active: false,
            plan,
            _ => { },
            () => { },
            () => { },
            () => syncCount++,
            () => { });

        result.SyncedOwners.Should().BeTrue();
        result.ShowedToolbarWindow.Should().BeFalse();
        syncCount.Should().Be(1);
    }
}
