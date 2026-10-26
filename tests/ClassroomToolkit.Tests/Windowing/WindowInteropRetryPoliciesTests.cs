using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class WindowInteropRetryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnMaxAttemptsReached_WhenAttemptReachedMax()
    {
        var decision = WindowInteropRetryPolicies.Resolve(attempt: 2, errorCode: 5);

        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().Be(WindowInteropRetryReason.MaxAttemptsReached);
    }

    [Theory]
    [InlineData(1400)]
    [InlineData(6)]
    public void Resolve_ShouldReturnInvalidHandleError_ForInvalidHandleErrors(int errorCode)
    {
        var decision = WindowInteropRetryPolicies.Resolve(attempt: 1, errorCode: errorCode);

        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().Be(WindowInteropRetryReason.InvalidHandleError);
    }

    [Fact]
    public void Resolve_ShouldReturnRetryableError_ForRecoverableErrorBeforeMaxAttempt()
    {
        var decision = WindowInteropRetryPolicies.Resolve(attempt: 1, errorCode: 5);

        decision.ShouldRetry.Should().BeTrue();
        decision.Reason.Should().Be(WindowInteropRetryReason.RetryableError);
    }
}
