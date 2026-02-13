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
}
