using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageRuntimeDefaultsTests
{
    [Fact]
    public void UnsetTimestampUtc_ShouldBeMinValue()
    {
        CrossPageRuntimeDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }
}
