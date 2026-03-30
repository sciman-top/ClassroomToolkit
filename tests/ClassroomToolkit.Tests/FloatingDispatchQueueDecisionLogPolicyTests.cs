using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class FloatingDispatchQueueDecisionLogPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ShouldLog_ShouldMatchExpected(int reason, bool expected)
    {
        var result = FloatingDispatchQueueDecisionLogPolicy.ShouldLog((FloatingDispatchQueueReason)reason);

        result.Should().Be(expected);
    }
}
