using ClassroomToolkit.Services.Input;
using FluentAssertions;
using System.Reflection;

namespace ClassroomToolkit.Tests;

public sealed class GlobalHookServiceTests
{
    [Fact]
    public async Task RegisterHookAsync_ShouldReturnFalse_WhenDisposed()
    {
        var service = new GlobalHookService();
        service.Dispose();

        var started = await service.RegisterHookAsync(
            bindingTokens: ["tab"],
            callback: () => { },
            shouldKeepActive: () => true);

        started.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterHookAsync_ShouldReturnFalse_WhenKeepActiveFalse()
    {
        var service = new GlobalHookService();
        try
        {
            var started = await service.RegisterHookAsync(
                bindingTokens: ["tab"],
                callback: () => { },
                shouldKeepActive: () => false);

            started.Should().BeFalse();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task RegisterHookAsync_ShouldReturnFalse_WhenAllTokensInvalid()
    {
        var service = new GlobalHookService();
        try
        {
            var started = await service.RegisterHookAsync(
                bindingTokens: ["capslock"],
                callback: () => { },
                shouldKeepActive: () => true);

            started.Should().BeFalse();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void UnregisterAll_ShouldBeIdempotent_AfterDispose()
    {
        var service = new GlobalHookService();
        service.Dispose();

        var act = () =>
        {
            service.UnregisterAll();
            service.UnregisterAll();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void NotifyHookUnavailable_ShouldNotBlockOtherSubscribers_WhenRecoverableCallbackThrows()
    {
        var service = new GlobalHookService();
        var callbackCount = 0;
        service.HookUnavailable += () => throw new InvalidOperationException("callback-boom");
        service.HookUnavailable += () => callbackCount++;

        try
        {
            InvokeNotifyHookUnavailable(service);
            callbackCount.Should().Be(1);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void NotifyHookUnavailable_ShouldRethrowFatalCallbackException()
    {
        var service = new GlobalHookService();
        service.HookUnavailable += () => throw new BadImageFormatException("fatal-callback");

        try
        {
            var act = () => InvokeNotifyHookUnavailable(service);
            act.Should().Throw<TargetInvocationException>()
                .Where(ex => ex.InnerException is BadImageFormatException);
        }
        finally
        {
            service.Dispose();
        }
    }

    private static void InvokeNotifyHookUnavailable(GlobalHookService service)
    {
        var method = typeof(GlobalHookService).GetMethod(
            "NotifyHookUnavailable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.Invoke(service, null);
    }
}
