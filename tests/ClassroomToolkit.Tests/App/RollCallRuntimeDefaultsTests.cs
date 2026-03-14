using System;
using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallRuntimeDefaultsTests
{
    [Fact]
    public void UnsetTimestampUtc_ShouldBeMinValue()
    {
        RollCallRuntimeDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ClassSwitchSuppressMs_ShouldMatchStabilizedValue()
    {
        RollCallRuntimeDefaults.ClassSwitchSuppressMs.Should().Be(250);
    }
}
