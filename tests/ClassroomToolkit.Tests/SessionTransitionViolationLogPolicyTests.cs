using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SessionTransitionViolationLogPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void ShouldLog_ShouldMatchExpected(int violationCount, bool expected)
    {
        SessionTransitionViolationLogPolicy.ShouldLog(violationCount).Should().Be(expected);
    }
}
