using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowDedupDefaultsTests
{
    [Fact]
    public void UnsetTimestampUtc_ShouldBeMinValue()
    {
        WindowDedupDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }
}
