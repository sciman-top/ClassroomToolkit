using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class LauncherWindowRuntimeSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldUseBubbleState_WhenLauncherIsMinimized()
    {
        var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(
            launcherMinimized: true,
            mainVisible: false,
            mainMinimized: true,
            mainActive: false,
            bubbleVisible: true,
            bubbleMinimized: false,
            bubbleActive: true);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Bubble);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.PreferBubbleVisible);
    }

    [Fact]
    public void Resolve_ShouldFallbackToMain_WhenLauncherMarkedMinimizedButBubbleNotVisible()
    {
        var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(
            launcherMinimized: true,
            mainVisible: true,
            mainMinimized: false,
            mainActive: true,
            bubbleVisible: false,
            bubbleMinimized: false,
            bubbleActive: false);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Main);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible);
    }

    [Fact]
    public void Resolve_ShouldUseMainWindowState_WhenLauncherIsNotMinimized()
    {
        var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: false,
            mainActive: true,
            bubbleVisible: false,
            bubbleMinimized: false,
            bubbleActive: false);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Main);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.PreferMainVisible);
    }

    [Fact]
    public void Resolve_ShouldFallbackToBubble_WhenMainTemporarilyHiddenAndBubbleVisible()
    {
        var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(
            launcherMinimized: false,
            mainVisible: false,
            mainMinimized: true,
            mainActive: false,
            bubbleVisible: true,
            bubbleMinimized: false,
            bubbleActive: true);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Bubble);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible);
    }

    [Fact]
    public void Resolve_ShouldTreatMinimizedBubbleAsNotVisible()
    {
        var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(
            launcherMinimized: true,
            mainVisible: false,
            mainMinimized: true,
            mainActive: false,
            bubbleVisible: true,
            bubbleMinimized: true,
            bubbleActive: true);

        snapshot.VisibleForTopmost.Should().BeFalse();
        snapshot.Active.Should().BeFalse();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Bubble);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible);
    }
}
