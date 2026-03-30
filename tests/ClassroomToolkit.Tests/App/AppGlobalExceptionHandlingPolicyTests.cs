using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class AppGlobalExceptionHandlingPolicyTests
{
    [Fact]
    public void ResolveForDispatcher_ShouldNotifyAndMarkHandled_ForRecoverableException()
    {
        var decision = AppGlobalExceptionHandlingPolicy.ResolveForDispatcher(
            new InvalidOperationException("recoverable"));

        decision.IsFatal.Should().BeFalse();
        decision.ShouldMarkDispatcherHandled.Should().BeTrue();
        decision.Action.Should().Be(AppGlobalExceptionAction.NotifyUser);
    }

    [Fact]
    public void ResolveForDispatcher_ShouldLogOnlyAndNotHandle_ForFatalException()
    {
        var decision = AppGlobalExceptionHandlingPolicy.ResolveForDispatcher(
            new BadImageFormatException("fatal"));

        decision.IsFatal.Should().BeTrue();
        decision.ShouldMarkDispatcherHandled.Should().BeFalse();
        decision.Action.Should().Be(AppGlobalExceptionAction.LogOnly);
    }

    [Fact]
    public void ResolveForBackground_ShouldLogOnly_ForRecoverableException()
    {
        var decision = AppGlobalExceptionHandlingPolicy.ResolveForBackground(
            new InvalidOperationException("background"));

        decision.IsFatal.Should().BeFalse();
        decision.ShouldMarkDispatcherHandled.Should().BeFalse();
        decision.Action.Should().Be(AppGlobalExceptionAction.LogOnly);
    }

    [Fact]
    public void IsNonFatal_ShouldReturnFalse_ForFatalException()
    {
        var result = AppGlobalExceptionHandlingPolicy.IsNonFatal(
            new BadImageFormatException("fatal"));

        result.Should().BeFalse();
    }
}
