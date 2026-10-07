using AwesomeAssertions;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class LauncherAutoExitTimerPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnStopPlan_WhenSecondsIsZero()
    {
        var plan = LauncherRuntimePolicies.ResolveLauncherAutoExitTimerPlan(autoExitSeconds: 0);

        plan.ShouldStart.Should().BeFalse();
        plan.Interval.Should().Be(System.TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_ShouldReturnStopPlan_WhenSecondsIsNegative()
    {
        var plan = LauncherRuntimePolicies.ResolveLauncherAutoExitTimerPlan(autoExitSeconds: -30);

        plan.ShouldStart.Should().BeFalse();
        plan.Interval.Should().Be(System.TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_ShouldReturnStartPlan_WhenSecondsIsPositive()
    {
        var plan = LauncherRuntimePolicies.ResolveLauncherAutoExitTimerPlan(autoExitSeconds: 90);

        plan.ShouldStart.Should().BeTrue();
        plan.Interval.Should().Be(System.TimeSpan.FromSeconds(90));
    }
}

public sealed class LauncherTopmostVisibilityTimestampPolicyTests
{
    [Fact]
    public void ResolveLastVisibleUtc_ShouldUseNow_WhenVisibleForTopmost()
    {
        var previous = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 3, 10, 10, 5, 0, DateTimeKind.Utc);

        var resolved = LauncherRuntimePolicies.ResolveLastVisibleUtc(
            previous,
            now,
            visibleForTopmost: true);

        resolved.Should().Be(now);
    }

    [Fact]
    public void ResolveLastVisibleUtc_ShouldKeepPrevious_WhenNotVisibleForTopmost()
    {
        var previous = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 3, 10, 10, 5, 0, DateTimeKind.Utc);

        var resolved = LauncherRuntimePolicies.ResolveLastVisibleUtc(
            previous,
            now,
            visibleForTopmost: false);

        resolved.Should().Be(previous);
    }
}

public sealed class LauncherWindowResolutionPolicyTests
{
    [Theory]
    [InlineData(1, true, true)]
    [InlineData(1, false, false)]
    [InlineData(0, true, false)]
    public void ShouldUseBubbleWindow_ShouldMatchExpected(
        int resolvedKind,
        bool bubbleWindowExists,
        bool expected)
    {
        var result = LauncherRuntimePolicies.ShouldUseBubbleWindow(
            (LauncherWindowKind)resolvedKind,
            bubbleWindowExists);

        result.Should().Be(expected);
    }
}

public sealed class LauncherWindowRuntimeSelectionLogPolicyTests
{
    [Theory]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible)]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible)]
    public void ShouldLog_ShouldReturnTrue_ForFallbackReasons(int reasonValue)
    {
        var reason = (LauncherWindowRuntimeSelectionReason)reasonValue;
        LauncherRuntimePolicies.ShouldLog(reason).Should().BeTrue();
    }

    [Theory]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.PreferMainVisible)]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.PreferBubbleVisible)]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.None)]
    public void ShouldLog_ShouldReturnFalse_ForNonFallbackReasons(int reasonValue)
    {
        var reason = (LauncherWindowRuntimeSelectionReason)reasonValue;
        LauncherRuntimePolicies.ShouldLog(reason).Should().BeFalse();
    }
}

public sealed class LauncherWorkAreaClampPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnOriginalPosition_WhenAlreadyInsideWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1080);

        var resolved = LauncherRuntimePolicies.ResolveLauncherWorkAreaClamp(
            left: 120,
            top: 80,
            width: 640,
            height: 480,
            workArea: workArea);

        resolved.X.Should().Be(120);
        resolved.Y.Should().Be(80);
    }

    [Fact]
    public void Resolve_ShouldClampToLeftTopBounds_WhenOutOfBounds()
    {
        var workArea = new Rect(10, 20, 1000, 700);

        var resolved = LauncherRuntimePolicies.ResolveLauncherWorkAreaClamp(
            left: -120,
            top: -90,
            width: 300,
            height: 200,
            workArea: workArea);

        resolved.X.Should().Be(10);
        resolved.Y.Should().Be(20);
    }

    [Fact]
    public void Resolve_ShouldClampToRightBottomBounds_WhenOverflow()
    {
        var workArea = new Rect(0, 0, 800, 600);

        var resolved = LauncherRuntimePolicies.ResolveLauncherWorkAreaClamp(
            left: 760,
            top: 590,
            width: 200,
            height: 120,
            workArea: workArea);

        resolved.X.Should().Be(600);
        resolved.Y.Should().Be(480);
    }
}
