using AwesomeAssertions;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Input;

namespace ClassroomToolkit.Tests;

/// <summary>
/// GlobalHookService 注册-回滚生命周期契约的行为级验证：使用注入的假句柄
/// 替代真实 WH_KEYBOARD_LL 安装，逐路径断言"已启动钩子必须被回滚、回调必须
/// 被解除、降级通知必须按路径发出"。
/// </summary>
[Trait("Gate", "CoreContract")]
public sealed class GlobalHookServiceLifecycleContractTests
{
    [Fact]
    public async Task RegisterHookAsync_ShouldRollbackStartedHooksAndNotify_WhenLaterHookStartThrows()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook();
            if (fakes.Count > 0)
            {
                fake.ThrowOnStart = true;
            }

            fakes.Add(fake);
            return fake;
        };
        var unavailableCount = 0;
        service.HookUnavailable += () => unavailableCount++;
        var callbackInvoked = false;
        try
        {
            var started = await service.RegisterHookAsync(
                bindings: TwoBindings(),
                callback: _ => callbackInvoked = true,
                shouldKeepActive: () => true);

            started.Should().BeFalse();
            fakes.Should().HaveCount(2);
            fakes[0].Disposed.Should().BeTrue("已启动的钩子必须在后续钩子启动失败时被回滚释放");
            fakes[0].Raise(fakes[0].BoundBinding!);
            callbackInvoked.Should().BeFalse("回滚必须解除已启动钩子上的回调");
            unavailableCount.Should().Be(1, "启动失败属于降级路径，必须通知 HookUnavailable");
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task RegisterHookAsync_ShouldRollbackStartedHooksAndNotify_WhenLaterHookIsInactive()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook { BoundBinding = binding };
            if (fakes.Count > 0)
            {
                fake.ActiveAfterStart = false;
            }

            fakes.Add(fake);
            return fake;
        };
        var unavailableCount = 0;
        service.HookUnavailable += () => unavailableCount++;
        try
        {
            var started = await service.RegisterHookAsync(
                bindings: TwoBindings(),
                callback: _ => { },
                shouldKeepActive: () => true);

            started.Should().BeFalse();
            fakes[0].Disposed.Should().BeTrue("StartAsync 完成但未激活的钩子必须触发整体回滚");
            unavailableCount.Should().Be(1);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task RegisterHookAsync_ShouldRollbackWithoutNotify_WhenKeepActiveTurnsFalseMidRegistration()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        var calls = 0;
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook { BoundBinding = binding };
            fakes.Add(fake);
            return fake;
        };
        var unavailableCount = 0;
        service.HookUnavailable += () => unavailableCount++;
        try
        {
            var started = await service.RegisterHookAsync(
                bindings: TwoBindings(),
                callback: _ => { },
                shouldKeepActive: () => ++calls <= 1);

            started.Should().BeFalse();
            fakes.Should().HaveCount(1);
            fakes[0].Disposed.Should().BeTrue("会话已失效时第一个钩子也必须被清理");
            unavailableCount.Should().Be(0, "主动停止不是钩子故障，不应触发 HookUnavailable 降级");
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task RegisterHookAsync_ShouldCleanupAndNotify_WhenBindingEnumerationThrows()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook { BoundBinding = binding };
            fakes.Add(fake);
            return fake;
        };
        var unavailableCount = 0;
        service.HookUnavailable += () => unavailableCount++;

        static async Task<bool> EnumerateThrowing(GlobalHookService target, List<FakeKeyboardHook> created)
        {
            return await target.RegisterHookAsync(
                bindings: ThrowAfterFirst(created),
                callback: _ => { },
                shouldKeepActive: () => true);
        }

        try
        {
            var started = await EnumerateThrowing(service, fakes);
            started.Should().BeFalse();
            fakes.Should().HaveCount(1);
            fakes[0].Disposed.Should().BeTrue("枚举中断时已启动的钩子必须被清理");
            unavailableCount.Should().Be(1);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task RegisterHookAsync_ShouldIsolateRecoverableBindingCallbackFailure()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook { BoundBinding = binding };
            fakes.Add(fake);
            return fake;
        };
        try
        {
            // 回调隔离由 token 重载统一包装（TryInvokeBindingCallback），
            // 与生产注册路径一致。
            var started = await service.RegisterHookAsync(
                bindingTokens: ["tab"],
                callback: () => throw new InvalidOperationException("binding-boom"),
                shouldKeepActive: () => true);

            started.Should().BeTrue();
            var act = () => fakes[0].Raise(fakes[0].BoundBinding!);
            act.Should().NotThrow("回调的可恢复异常必须被隔离，不得冒泡进钩子回调链");
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Dispose_ShouldStopAllTrackedHooks_AndSuppressFurtherCallbacks()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook { BoundBinding = binding, ClearHandlersOnDispose = true };
            fakes.Add(fake);
            return fake;
        };
        var callbackInvoked = false;
        try
        {
            var started = await service.RegisterHookAsync(
                bindings: TwoBindings(),
                callback: _ => callbackInvoked = true,
                shouldKeepActive: () => true);

            started.Should().BeTrue();
            service.Dispose();
            fakes.Should().OnlyContain(fake => fake.Disposed, "Dispose 必须停止全部已跟踪钩子");
            fakes[0].Raise(fakes[0].BoundBinding!);
            callbackInvoked.Should().BeFalse();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task UnregisterAll_ShouldStopTrackedHooks_AndTolerateRecoverableStopFailures()
    {
        var service = new GlobalHookService();
        var fakes = new List<FakeKeyboardHook>();
        service.HookFactory = binding =>
        {
            var fake = new FakeKeyboardHook { BoundBinding = binding, ThrowOnDispose = true };
            fakes.Add(fake);
            return fake;
        };
        try
        {
            var started = await service.RegisterHookAsync(
                bindings: TwoBindings(),
                callback: _ => { },
                shouldKeepActive: () => true);

            started.Should().BeTrue();
            var act = () => service.UnregisterAll();
            act.Should().NotThrow("单个钩子停止失败不得中断其余钩子的清理");
        }
        finally
        {
            service.Dispose();
        }
    }

    private static IEnumerable<KeyBinding> TwoBindings() =>
    [
        new KeyBinding(VirtualKey.Tab, KeyModifiers.None),
        new KeyBinding(VirtualKey.Escape, KeyModifiers.None)
    ];

    private static IEnumerable<KeyBinding> ThrowAfterFirst(List<FakeKeyboardHook> created)
    {
        yield return new KeyBinding(VirtualKey.Tab, KeyModifiers.None);
        _ = created;
        throw new InvalidOperationException("enumeration-boom");
    }

    private sealed class FakeKeyboardHook : IKeyboardHookHandle
    {
        private Action<KeyBinding>? _triggered;

        public KeyBinding? BoundBinding { get; init; }
        public bool ActiveAfterStart { get; set; } = true;
        public bool ThrowOnStart { get; set; }
        public bool ThrowOnDispose { get; set; }
        public bool ClearHandlersOnDispose { get; set; }
        public bool Disposed { get; private set; }
        public bool IsActive { get; private set; }

        public event Action<KeyBinding>? BindingTriggered
        {
            add => _triggered += value;
            remove => _triggered -= value;
        }

        public Task StartAsync()
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("start-boom");
            }

            IsActive = ActiveAfterStart;
            return Task.CompletedTask;
        }

        public void Raise(KeyBinding binding)
        {
            _triggered?.Invoke(binding);
        }

        public void Dispose()
        {
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("dispose-boom");
            }

            if (ClearHandlersOnDispose)
            {
                // 模拟真实 KeyboardHook.Stop() 的语义：停止后不再触发绑定回调。
                _triggered = null;
            }

            Disposed = true;
        }
    }
}
