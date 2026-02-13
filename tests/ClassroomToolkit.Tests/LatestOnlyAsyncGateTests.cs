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
}
