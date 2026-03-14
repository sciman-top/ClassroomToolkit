using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherTopmostVisibilityHoldPolicyTests
{
    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnTrue_WhenCurrentlyVisible()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherTopmostVisibilityHoldPolicy.ResolveVisibleForRepair(
            currentVisibleForTopmost: true,
            lastVisibleForTopmostUtc: DateTime.MinValue,
            nowUtc: nowUtc);

        visible.Should().BeTrue();
    }

    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnFalse_WhenNeverVisible()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherTopmostVisibilityHoldPolicy.ResolveVisibleForRepair(
            currentVisibleForTopmost: false,
            lastVisibleForTopmostUtc: DateTime.MinValue,
            nowUtc: nowUtc);

        visible.Should().BeFalse();
    }

    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnTrue_WithinHoldWindow()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherTopmostVisibilityHoldPolicy.ResolveVisibleForRepair(
            currentVisibleForTopmost: false,
            lastVisibleForTopmostUtc: nowUtc.AddMilliseconds(-120),
            nowUtc: nowUtc,
            holdMs: 180);

        visible.Should().BeTrue();
    }

    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnFalse_AfterHoldWindow()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherTopmostVisibilityHoldPolicy.ResolveVisibleForRepair(
            currentVisibleForTopmost: false,
            lastVisibleForTopmostUtc: nowUtc.AddMilliseconds(-260),
            nowUtc: nowUtc,
            holdMs: 180);

        visible.Should().BeFalse();
    }
}
