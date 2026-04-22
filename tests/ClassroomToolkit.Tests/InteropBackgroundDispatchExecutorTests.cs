using System;
using System.Threading;
using ClassroomToolkit.Interop.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropBackgroundDispatchExecutorTests
{
    [Fact]
    public void Queue_ShouldExecuteAction()
    {
        using var completed = new ManualResetEventSlim(false);
        var cancellationToken = TestContext.Current.CancellationToken;

        InteropBackgroundDispatchExecutor.Queue(
            "test-execute",
            () => completed.Set());

        completed.Wait(TimeSpan.FromSeconds(2), cancellationToken).Should().BeTrue();
    }

    [Fact]
    public void Queue_WhenActionThrows_ShouldInvokeErrorCallback()
    {
        using var completed = new ManualResetEventSlim(false);
        var cancellationToken = TestContext.Current.CancellationToken;
        Exception? captured = null;

        InteropBackgroundDispatchExecutor.Queue(
            "test-failure",
            () => throw new InvalidOperationException("boom"),
            ex =>
            {
                captured = ex;
                completed.Set();
            });

        completed.Wait(TimeSpan.FromSeconds(2), cancellationToken).Should().BeTrue();
        captured.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Queue_WhenErrorCallbackThrows_ShouldNotBlockSubsequentQueueWork()
    {
        using var callbackCompleted = new ManualResetEventSlim(false);
        using var subsequentCompleted = new ManualResetEventSlim(false);
        var cancellationToken = TestContext.Current.CancellationToken;
        var errorCallbackCount = 0;

        InteropBackgroundDispatchExecutor.Queue(
            "test-onerror-throws",
            () => throw new InvalidOperationException("boom"),
            _ =>
            {
                Interlocked.Increment(ref errorCallbackCount);
                callbackCompleted.Set();
                throw new InvalidOperationException("on-error failed");
            });

        InteropBackgroundDispatchExecutor.Queue(
            "test-subsequent",
            () => subsequentCompleted.Set());

        callbackCompleted.Wait(TimeSpan.FromSeconds(2), cancellationToken).Should().BeTrue();
        subsequentCompleted.Wait(TimeSpan.FromSeconds(2), cancellationToken).Should().BeTrue();
        errorCallbackCount.Should().Be(1);
    }
}
