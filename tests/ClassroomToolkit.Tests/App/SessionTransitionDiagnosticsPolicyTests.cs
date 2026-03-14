using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDiagnosticsPolicyTests
{
    [Fact]
    public void FormatAdmissionSkipMessage_ShouldContainIdAndReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatAdmissionSkipMessage(
            transitionId: 42,
            reason: SessionTransitionAdmissionReason.DuplicateTransitionId);

        message.Should().Contain("[UiSession][Admission] skip #42");
        message.Should().Contain("reason=duplicate-transition-id");
    }

    [Fact]
    public void FormatApplyGateSkipMessage_ShouldContainIdAndReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatApplyGateSkipMessage(
            transitionId: 43,
            reason: SessionTransitionApplyGateReason.NoZOrderAction);

        message.Should().Contain("[UiSession][ApplyGate] skip #43");
        message.Should().Contain("reason=no-zorder-action");
    }

    [Fact]
    public void FormatDuplicateResetMessage_ShouldContainReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatDuplicateResetMessage(
            SessionTransitionDuplicateResetReason.NoAppliedTransition);

        message.Should().Contain("[UiSession][DuplicateReset]");
        message.Should().Contain("reason=no-applied-transition");
    }

    [Fact]
    public void FormatWindowingReasonMessage_ShouldContainTransitionIdAndReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatWindowingReasonMessage(
            transitionId: 44,
            reason: SessionTransitionWindowingReason.WidgetBecameVisible);

        message.Should().Contain("[UiSession][Windowing] #44");
        message.Should().Contain("reason=widget-became-visible");
    }

    [Fact]
    public void FormatWidgetVisibilityReasonMessage_ShouldContainTransitionIdAndReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatWidgetVisibilityReasonMessage(
            transitionId: 45,
            reason: SessionFloatingWidgetVisibilityReason.LauncherBecameVisible);

        message.Should().Contain("[UiSession][WidgetVisibility] #45");
        message.Should().Contain("reason=launcher-became-visible");
    }

    [Fact]
    public void FormatApplyReasonMessage_ShouldContainTransitionIdAndReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatApplyReasonMessage(
            transitionId: 46,
            reason: SessionTransitionApplyReason.WidgetBecameVisible);

        message.Should().Contain("[UiSession][Apply] #46");
        message.Should().Contain("reason=widget-became-visible");
    }

    [Fact]
    public void FormatSurfaceReasonMessage_ShouldContainTransitionIdAndReasonTag()
    {
        var message = SessionTransitionDiagnosticsPolicy.FormatSurfaceReasonMessage(
            transitionId: 47,
            reason: SessionTransitionSurfaceReason.SurfaceRetouchRequested);

        message.Should().Contain("[UiSession][Surface] #47");
        message.Should().Contain("reason=surface-retouch-requested");
    }
}
