using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStyleInteropRetryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnFalse_WhenAttemptReachedMax()
    {
        var decision = WindowStyleInteropRetryPolicy.Resolve(attempt: 2, errorCode: 5);

        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().Be(WindowStyleInteropRetryReason.MaxAttemptsReached);
    }

    [Theory]
    [InlineData(1400)]
    [InlineData(6)]
    public void Resolve_ShouldReturnFalse_ForInvalidHandleErrors(int errorCode)
    {
        var decision = WindowStyleInteropRetryPolicy.Resolve(attempt: 1, errorCode: errorCode);

        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().Be(WindowStyleInteropRetryReason.InvalidHandleError);
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_ForRecoverableErrorBeforeMaxAttempt()
    {
        var decision = WindowStyleInteropRetryPolicy.Resolve(attempt: 1, errorCode: 5);

        decision.ShouldRetry.Should().BeTrue();
        decision.Reason.Should().Be(WindowStyleInteropRetryReason.RetryableError);
    }

    [Fact]
    public void ShouldRetry_ShouldMapResolveDecision()
    {
        var shouldRetry = WindowStyleInteropRetryPolicy.ShouldRetry(attempt: 1, errorCode: 5);

        shouldRetry.Should().BeTrue();
    }
}
