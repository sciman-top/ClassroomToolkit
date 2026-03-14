using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchDiagnosticsPolicyTests
{
    [Fact]
    public void FormatDecisionSkipMessage_ShouldContainTriggerAndReason()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatDecisionSkipMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionRetouchDecisionReason.NoTopmostDrift);

        message.Should().Contain("[ToolbarRetouch][Decision] skip");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("reason=no-topmost-drift");
    }

    [Fact]
    public void FormatActivationSuppressionSkipMessage_ShouldContainTriggerAndReason()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatActivationSuppressionSkipMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionActivationSuppressionReason.LauncherOnlyDrift);

        message.Should().Contain("[ToolbarRetouch][Suppression] skip");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("reason=launcher-only-drift");
    }

    [Fact]
    public void FormatAdmissionSkipMessage_ShouldContainTriggerReasonAndForceFlag()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatAdmissionSkipMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionRetouchAdmissionReason.ReentryBlocked,
            forceEnforceZOrder: false);

        message.Should().Contain("[ToolbarRetouch][Admission] skip");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("reason=reentry-blocked");
        message.Should().Contain("force=False");
    }

    [Fact]
    public void FormatThrottleSkipMessage_ShouldContainTriggerReasonAndInterval()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatThrottleSkipMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionRetouchThrottleReason.WithinThrottleWindow,
            minimumIntervalMs: 120);

        message.Should().Contain("[ToolbarRetouch][Throttle] skip");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("reason=within-throttle-window");
        message.Should().Contain("minIntervalMs=120");
    }

    [Fact]
    public void FormatExecutionPlanMessage_ShouldContainTriggerAndPlanFlags()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatExecutionPlanMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        message.Should().Contain("[ToolbarRetouch][Execute]");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("directRepair=True");
        message.Should().Contain("requestZOrder=False");
        message.Should().Contain("force=False");
    }

    [Fact]
    public void FormatDirectRepairAdmissionSkipMessage_ShouldContainTriggerAndReason()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairAdmissionSkipMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued);

        message.Should().Contain("[ToolbarRetouch][DirectRepair] skip");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("reason=zorder-queued");
    }

    [Fact]
    public void FormatDirectRepairDispatchMessage_ShouldContainTriggerAndMode()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionRetouchDispatchMode.Background);

        message.Should().Contain("[ToolbarRetouch][DirectRepair] dispatch");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("mode=Background");
    }

    [Fact]
    public void FormatDirectRepairDispatchAdmissionSkipMessage_ShouldContainTriggerAndReason()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchAdmissionSkipMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            ToolbarInteractionDirectRepairDispatchAdmissionReason.AlreadyQueued);

        message.Should().Contain("[ToolbarRetouch][DirectRepair] dispatch-skip");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("reason=already-queued");
    }

    [Fact]
    public void FormatDirectRepairDispatchFailureMessage_ShouldContainTriggerAndExceptionDetails()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchFailureMessage(
            ToolbarInteractionRetouchTrigger.Activated,
            "InvalidOperationException",
            "dispatcher failed");

        message.Should().Contain("[ToolbarRetouch][DirectRepair] dispatch-failed");
        message.Should().Contain("trigger=Activated");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=dispatcher failed");
    }

    [Fact]
    public void FormatRuntimeResetMessage_ShouldContainReasonTag()
    {
        var message = ToolbarInteractionRetouchDiagnosticsPolicy.FormatRuntimeResetMessage(
            ToolbarInteractionRetouchRuntimeResetReason.RequestExit);

        message.Should().Contain("[ToolbarRetouch][RuntimeReset]");
        message.Should().Contain("reason=request-exit");
    }
}
