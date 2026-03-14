using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestQueuedDiagnosticsAdmissionPolicyTests
{
    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(4, false)]
    [InlineData(1, false)]
    public void ShouldLog_ShouldMatchExpectedReasons(int reasonValue, bool expected)
    {
        var reason = (ZOrderRequestAdmissionReason)reasonValue;
        ZOrderRequestQueuedDiagnosticsAdmissionPolicy.ShouldLog(reason).Should().Be(expected);
    }
}
