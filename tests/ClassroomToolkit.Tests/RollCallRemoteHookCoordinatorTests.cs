using ClassroomToolkit.App.RollCall;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallRemoteHookCoordinatorTests
{
    [Fact]
    public async Task TryStartAsync_ShouldSkipRegistration_WhenHookDisabled()
    {
        var registerCalls = 0;
        var coordinator = CreateCoordinator(
            onRegister: (_, _, _) =>
            {
                registerCalls++;
                return Task.FromResult(true);
            });

        var result = await coordinator.TryStartAsync(new RollCallRemoteHookStartRequest(
            ShouldEnable: false,
            ConfiguredKey: "tab",
            FallbackToken: "tab",
            Handler: () => { },
            ShouldKeepActive: () => true,
            AlreadyUnavailableNotified: false,
            NotifyUnavailableOnFailure: true));

        result.Started.Should().BeFalse();
        result.ShouldNotifyUnavailable.Should().BeFalse();
        registerCalls.Should().Be(0);
    }

    [Fact]
    public async Task TryStartAsync_ShouldSkipRegistration_WhenNoLongerActive()
    {
        var registerCalls = 0;
        var coordinator = CreateCoordinator(
            onRegister: (_, _, _) =>
            {
                registerCalls++;
                return Task.FromResult(true);
            });

        var result = await coordinator.TryStartAsync(new RollCallRemoteHookStartRequest(
            ShouldEnable: true,
            ConfiguredKey: "tab",
            FallbackToken: "tab",
            Handler: () => { },
            ShouldKeepActive: () => false,
            AlreadyUnavailableNotified: false,
            NotifyUnavailableOnFailure: true));

        result.Started.Should().BeFalse();
        result.ShouldNotifyUnavailable.Should().BeFalse();
        registerCalls.Should().Be(0);
    }

    [Fact]
    public async Task TryStartAsync_ShouldNotifyUnavailable_WhenRegistrationFailsAndNotNotified()
    {
        var registerCalls = 0;
        var coordinator = CreateCoordinator(
            onRegister: (_, _, _) =>
            {
                registerCalls++;
                return Task.FromResult(false);
            });

        var result = await coordinator.TryStartAsync(new RollCallRemoteHookStartRequest(
            ShouldEnable: true,
            ConfiguredKey: "tab",
            FallbackToken: "tab",
            Handler: () => { },
            ShouldKeepActive: () => true,
            AlreadyUnavailableNotified: false,
            NotifyUnavailableOnFailure: true));

        result.Started.Should().BeFalse();
        result.ShouldNotifyUnavailable.Should().BeTrue();
        registerCalls.Should().Be(1);
    }

    [Fact]
    public async Task TryStartAsync_ShouldNotNotify_WhenNotifyFlagDisabled()
    {
        var registerCalls = 0;
        var coordinator = CreateCoordinator(
            onRegister: (_, _, _) =>
            {
                registerCalls++;
                return Task.FromResult(false);
            });

        var result = await coordinator.TryStartAsync(new RollCallRemoteHookStartRequest(
            ShouldEnable: true,
            ConfiguredKey: "b",
            FallbackToken: "b",
            Handler: () => { },
            ShouldKeepActive: () => true,
            AlreadyUnavailableNotified: false,
            NotifyUnavailableOnFailure: false));

        result.Started.Should().BeFalse();
        result.ShouldNotifyUnavailable.Should().BeFalse();
        registerCalls.Should().Be(1);
    }

    [Fact]
    public async Task TryStartAsync_ShouldRegisterF5TripleBindings()
    {
        IReadOnlyList<string>? capturedBindings = null;
        var coordinator = CreateCoordinator(
            onRegister: (bindings, _, _) =>
            {
                capturedBindings = bindings.ToArray();
                return Task.FromResult(true);
            });

        var result = await coordinator.TryStartAsync(new RollCallRemoteHookStartRequest(
            ShouldEnable: true,
            ConfiguredKey: "f5",
            FallbackToken: "tab",
            Handler: () => { },
            ShouldKeepActive: () => true,
            AlreadyUnavailableNotified: false,
            NotifyUnavailableOnFailure: true));

        result.Started.Should().BeTrue();
        result.ShouldNotifyUnavailable.Should().BeFalse();
        capturedBindings.Should().NotBeNull();
        capturedBindings!.Count.Should().Be(3);
        capturedBindings[0].Should().Be("f5");
        capturedBindings[1].Should().Be("shift+f5");
        capturedBindings[2].Should().Be("escape");
    }

    [Fact]
    public void StopAllHooks_ShouldInvokeUnregisterAction()
    {
        var unregisterCalls = 0;
        var coordinator = CreateCoordinator(
            onRegister: (_, _, _) => Task.FromResult(true),
            onUnregister: () => unregisterCalls++);

        coordinator.StopAllHooks();

        unregisterCalls.Should().Be(1);
    }

    private static RollCallRemoteHookCoordinator CreateCoordinator(
        Func<IEnumerable<string>, Action, Func<bool>, Task<bool>> onRegister,
        Action? onUnregister = null)
    {
        return new RollCallRemoteHookCoordinator(
            registerHookAsync: onRegister,
            resolveBindings: RollCallRemoteHookBindingPolicy.ResolveTokens,
            unregisterAll: onUnregister ?? (() => { }));
    }
}
