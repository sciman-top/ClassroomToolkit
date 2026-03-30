using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateThrottleDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs.Should().Be(0);
        CrossPageDisplayUpdateThrottleDefaults.MinDelayedDispatchMs.Should().Be(1);
    }
}
