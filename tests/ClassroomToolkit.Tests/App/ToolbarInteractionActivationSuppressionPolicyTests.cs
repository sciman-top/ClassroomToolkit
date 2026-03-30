using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionActivationSuppressionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNonActivatedTrigger_ForNonActivatedTrigger()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.PreviewMouseDown,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc,
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.NonActivatedTrigger);
    }

    [Fact]
    public void Resolve_ShouldReturnPreviewTimestampUnset_WhenPreviewTimestampUnset()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: WindowDedupDefaults.UnsetTimestampUtc,
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.PreviewTimestampUnset);
    }

    [Fact]
    public void Resolve_ShouldNotSuppress_WhenWithinSuppressionWindow_AndLauncherOnlyDrift()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-40),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnOutsideSuppressionWindow_WhenOutsideSuppressionWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-200),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.OutsideSuppressionWindow);
    }

    [Fact]
    public void Resolve_ShouldReturnNotSuppressed_WhenToolbarAlsoDrifts()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false, toolbarTopmost: false);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-40),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.None);
    }

    [Fact]
    public void ShouldSuppress_ShouldMapResolveDecision()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        ToolbarInteractionActivationSuppressionPolicy.ShouldSuppress(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-40),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90).Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldSuppressActivated_WhenPreviewAlreadyRetouchedInWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: true, toolbarTopmost: true);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-20),
            lastRetouchUtc: nowUtc.AddMilliseconds(-10),
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.PreviewAlreadyRetouched);
    }

    [Fact]
    public void Resolve_ShouldNotSuppress_WhenRetouchIsEarlierThanPreview()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: true, toolbarTopmost: true);

        var decision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-10),
            lastRetouchUtc: nowUtc.AddMilliseconds(-20),
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.None);
    }

    private static ToolbarInteractionRetouchSnapshot CreateSnapshot(
        bool launcherTopmost,
        bool toolbarTopmost = true)
    {
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: toolbarTopmost,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: launcherTopmost);
    }
}
