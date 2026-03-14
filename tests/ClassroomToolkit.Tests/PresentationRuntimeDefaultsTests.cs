using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationRuntimeDefaultsTests
{
    [Fact]
    public void FocusTimingDefaults_ShouldMatchStabilizedValues()
    {
        PresentationRuntimeDefaults.FocusMonitorIntervalMs.Should().Be(500);
        PresentationRuntimeDefaults.FocusRestoreCooldownMs.Should().Be(1200);
    }

    [Fact]
    public void WpsDebounceDefault_ShouldMatchStabilizedValue()
    {
        PresentationRuntimeDefaults.WpsNavDebounceMs.Should().Be(200);
        PresentationRuntimeDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }
}
