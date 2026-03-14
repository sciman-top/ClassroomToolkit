using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldUpdateTrackedRequestState()
    {
        var state = ZOrderRequestRuntimeState.Default;
        var now = DateTime.UtcNow;
        var admission = new ZOrderRequestAdmissionDecision(
            ShouldQueue: true,
            LastRequestUtc: now,
            LastForceEnforceZOrder: true,
            Reason: ZOrderRequestAdmissionReason.None);

        ZOrderRequestStateUpdater.Apply(ref state, admission);

        state.LastRequestUtc.Should().Be(now);
        state.LastForceEnforceZOrder.Should().BeTrue();
    }
}
