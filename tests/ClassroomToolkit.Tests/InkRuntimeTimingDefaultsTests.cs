using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkRuntimeTimingDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkRuntimeTimingDefaults.CalligraphyPreviewMinIntervalMs.Should().Be(16);
        InkRuntimeTimingDefaults.InputCooldownMs.Should().Be(120);
        InkRuntimeTimingDefaults.MonitorActiveIntervalMs.Should().Be(600);
        InkRuntimeTimingDefaults.MonitorIdleIntervalMs.Should().Be(1400);
        InkRuntimeTimingDefaults.IdleThresholdMs.Should().Be(2500);
        InkRuntimeTimingDefaults.RedrawMinIntervalMs.Should().Be(16);
        InkRuntimeTimingDefaults.PhotoPanRedrawThresholdDip.Should().Be(3);
        InkRuntimeTimingDefaults.RedrawDispatchDelayMinMs.Should().Be(1);
        InkRuntimeTimingDefaults.SidecarAutoSaveDelayMs.Should().Be(600);
        InkRuntimeTimingDefaults.SidecarAutoSaveRetryMax.Should().Be(3);
        InkRuntimeTimingDefaults.SidecarAutoSaveRetryDelayMs.Should().Be(900);
        InkRuntimeTimingDefaults.CalligraphyAdaptiveAdjustMinIntervalMs.Should().Be(200);
        InkRuntimeTimingDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }
}
