using System.Threading;
using ClassroomToolkit.App.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SafeTaskRunnerTests
{
    [Fact]
    public async Task Run_ShouldExecuteAction()
    {
        var invoked = false;

        await SafeTaskRunner.Run("test.run.execute", _ =>
        {
            invoked = true;
        });

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task Run_ShouldInvokeOnError_WhenActionThrows()
    {
        Exception? captured = null;

        await SafeTaskRunner.Run(
            "test.run.error",
            _ => throw new InvalidOperationException("boom"),
            onError: ex => captured = ex);

        captured.Should().NotBeNull();
        captured.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Run_ShouldSkipAction_WhenCancellationAlreadyRequested()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;

        await SafeTaskRunner.Run(
            "test.run.canceled",
            _ => invoked = true,
            cancellation.Token);

        invoked.Should().BeFalse();
    }

    [Fact]
    public void Run_ShouldReturnCompletedTaskImmediately_WhenCancellationAlreadyRequested()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var task = SafeTaskRunner.Run(
            "test.run.canceled.immediate",
            _ => throw new InvalidOperationException("should not run"),
            cancellation.Token);

        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Run_ShouldNotInvokeOnError_WhenOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var onErrorCalled = false;

        await SafeTaskRunner.Run(
            "test.run.canceled.noerror",
            _ => throw new OperationCanceledException(cancellation.Token),
            cancellation.Token,
            _ => onErrorCalled = true);

        onErrorCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Run_ShouldSwallowRecoverableOnErrorException()
    {
        Func<Task> act = async () =>
        {
            await SafeTaskRunner.Run(
                "test.run.onerror.recoverable",
                _ => throw new InvalidOperationException("boom"),
                onError: _ => throw new ApplicationException("callback-failed"));
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Run_ShouldRethrowFatalException_WhenActionThrowsFatal()
    {
        var task = SafeTaskRunner.Run(
            "test.run.fatal",
            _ => throw new BadImageFormatException("fatal"));

        await task.Awaiting(t => t).Should().ThrowAsync<BadImageFormatException>();
    }

    [Fact]
    public async Task Run_ShouldRethrowFatalException_WhenOnErrorThrowsFatal()
    {
        var task = SafeTaskRunner.Run(
            "test.run.onerror.fatal",
            _ => throw new InvalidOperationException("boom"),
            onError: _ => throw new BadImageFormatException("fatal-callback"));

        await task.Awaiting(t => t).Should().ThrowAsync<BadImageFormatException>();
    }
}
