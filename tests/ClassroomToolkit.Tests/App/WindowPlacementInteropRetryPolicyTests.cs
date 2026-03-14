using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowPlacementInteropRetryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnMaxAttemptsReached_WhenAttemptReachedMax()
    {
        var decision = WindowPlacementInteropRetryPolicy.Resolve(attempt: 2, errorCode: 5);

        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().Be(WindowPlacementInteropRetryReason.MaxAttemptsReached);
    }

    [Theory]
    [InlineData(1400)]
    [InlineData(6)]
    public void Resolve_ShouldReturnInvalidHandleError_ForInvalidHandleErrors(int errorCode)
    {
        var decision = WindowPlacementInteropRetryPolicy.Resolve(attempt: 1, errorCode: errorCode);

        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().Be(WindowPlacementInteropRetryReason.InvalidHandleError);
    }

    [Fact]
    public void Resolve_ShouldReturnRetryableError_ForRecoverableErrorBeforeMaxAttempt()
    {
        var decision = WindowPlacementInteropRetryPolicy.Resolve(attempt: 1, errorCode: 5);

        decision.ShouldRetry.Should().BeTrue();
        decision.Reason.Should().Be(WindowPlacementInteropRetryReason.RetryableError);
    }

    [Fact]
    public void ShouldRetry_ShouldMapResolveDecision()
    {
        var shouldRetry = WindowPlacementInteropRetryPolicy.ShouldRetry(attempt: 1, errorCode: 5);

        shouldRetry.Should().BeTrue();
    }
}
