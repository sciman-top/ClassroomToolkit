using ClassroomToolkit.App.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LatestOnlyAsyncGateTests
{
    [Fact]
    public async Task RunAsync_ShouldSkipStaleGeneration()
    {
        var gate = new LatestOnlyAsyncGate();
        var stale = gate.NextGeneration();
        var current = gate.NextGeneration();
        var staleRan = false;
        var currentRan = false;

        await gate.RunAsync(stale, _ =>
        {
            staleRan = true;
            return Task.CompletedTask;
        });

        await gate.RunAsync(current, _ =>
        {
            currentRan = true;
            return Task.CompletedTask;
        });

        staleRan.Should().BeFalse();
        currentRan.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ShouldExposeCurrentStateInsideAction()
    {
        var gate = new LatestOnlyAsyncGate();
        var generation = gate.NextGeneration();
        var before = false;
        var after = true;

        await gate.RunAsync(generation, async isCurrent =>
        {
            before = isCurrent();
            gate.NextGeneration();
            await Task.Delay(1);
            after = isCurrent();
        });

        before.Should().BeTrue();
        after.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ShouldSerializeConcurrentRequests()
    {
        var gate = new LatestOnlyAsyncGate();
        var generation = gate.NextGeneration();
        var running = 0;
        var maxRunning = 0;

        Task RunOne() => gate.RunAsync(generation, async _ =>
        {
            var current = Interlocked.Increment(ref running);
            if (current > maxRunning)
            {
                maxRunning = current;
            }
            await Task.Delay(20);
            Interlocked.Decrement(ref running);
        });

        await Task.WhenAll(RunOne(), RunOne(), RunOne());

        maxRunning.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_ShouldNoop_WhenDisposed()
    {
        var gate = new LatestOnlyAsyncGate();
        var generation = gate.NextGeneration();
        gate.Dispose();
        var ran = false;

        await gate.RunAsync(generation, _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        ran.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var gate = new LatestOnlyAsyncGate();

        var act = () =>
        {
            gate.Dispose();
            gate.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_ShouldCompleteQueuedOperationWithoutRunningIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var gate = new LatestOnlyAsyncGate();
        var generation = gate.NextGeneration();
        var actionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAction = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedActionRan = false;
        var actionStillCurrentAfterDispose = true;

        var active = gate.RunAsync(generation, async isCurrent =>
        {
            actionStarted.SetResult(true);
            await releaseAction.Task;
            actionStillCurrentAfterDispose = isCurrent();
        });
        await actionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        var queued = gate.RunAsync(generation, _ =>
        {
            queuedActionRan = true;
            return Task.CompletedTask;
        });

        gate.Dispose();
        releaseAction.SetResult(true);

        await active.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        await queued.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

        queuedActionRan.Should().BeFalse();
        actionStillCurrentAfterDispose.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ShouldThrowArgumentNullException_WhenActionIsNull()
    {
        var gate = new LatestOnlyAsyncGate();
        var generation = gate.NextGeneration();

        var act = () => gate.RunAsync(generation, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
