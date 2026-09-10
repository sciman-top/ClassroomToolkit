using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookOrchestratorTests
{
    [Fact]
    public void ApplyEnabled_ShouldConfigureHookAndReturnRuntimeState()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();
        var decision = new WpsHookInterceptDecision(
            InterceptKeyboard: false,
            InterceptWheel: true,
            BlockOnly: true,
            EmitWheelOnBlock: false);

        var state = orchestrator.ApplyEnabled(hook, decision, currentActive: false);

        state.IsActive.Should().BeFalse();
        state.BlockOnly.Should().BeTrue();
        state.InterceptKeyboard.Should().BeFalse();
        state.InterceptWheel.Should().BeTrue();
        hook.InterceptEnabled.Should().BeTrue();
        hook.BlockOnly.Should().BeTrue();
        hook.InterceptKeyboard.Should().BeFalse();
        hook.InterceptWheel.Should().BeTrue();
        hook.EmitWheelOnBlock.Should().BeFalse();
    }

    [Fact]
    public void ApplyEnabled_ShouldFailClosed_WhenHookConfigurationThrowsNonFatal()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient
        {
            ConfigurationException = new InvalidOperationException("set-failed")
        };
        var decision = new WpsHookInterceptDecision(
            InterceptKeyboard: false,
            InterceptWheel: true,
            BlockOnly: true,
            EmitWheelOnBlock: false);

        var state = default(WpsHookRuntimeState);
        var act = () => state = orchestrator.ApplyEnabled(hook, decision, currentActive: true);

        act.Should().NotThrow();
        state.IsActive.Should().BeFalse();
        state.BlockOnly.Should().BeFalse();
        state.InterceptKeyboard.Should().BeTrue();
        state.InterceptWheel.Should().BeTrue();
        state.ConfigurationApplied.Should().BeFalse();
        hook.StopCalled.Should().BeTrue();
    }

    [Fact]
    public void ApplyDisabled_ShouldResetHookState()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();

        var state = orchestrator.ApplyDisabled(hook);

        state.IsActive.Should().BeFalse();
        state.BlockOnly.Should().BeFalse();
        state.InterceptKeyboard.Should().BeTrue();
        state.InterceptWheel.Should().BeTrue();
        hook.InterceptEnabled.Should().BeFalse();
        hook.BlockOnly.Should().BeFalse();
        hook.InterceptKeyboard.Should().BeTrue();
        hook.InterceptWheel.Should().BeTrue();
        hook.EmitWheelOnBlock.Should().BeTrue();
        hook.StopCalled.Should().BeTrue();
    }

    [Fact]
    public void ApplyDisabled_ShouldClearSuppressedKeyboardKeys()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();

        orchestrator.ApplyDisabled(hook);

        hook.SuppressedKeyboardKeys.Should().BeEmpty();
    }

    [Fact]
    public void ApplyEnabled_ShouldReapplyReservedKeys_AfterDisableEnableCycle()
    {
        // 回归：保留键只由点名侧事件写入；overlay 显隐/模式切换会走 ApplyDisabled→ApplyEnabled，
        // 若重启用不回填，Enter 会同时切换点名分组并注入 WPS 翻页。
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();
        orchestrator.SetReservedPresentationKeys([VirtualKey.Enter]);
        orchestrator.ApplyEnabled(
            hook,
            new WpsHookInterceptDecision(InterceptKeyboard: false, InterceptWheel: true, BlockOnly: false, EmitWheelOnBlock: false),
            currentActive: false);
        hook.SuppressedKeyboardKeys.Should().Equal(VirtualKey.Enter);

        orchestrator.ApplyDisabled(hook);
        hook.SuppressedKeyboardKeys.Should().BeEmpty();

        orchestrator.ApplyEnabled(
            hook,
            new WpsHookInterceptDecision(InterceptKeyboard: false, InterceptWheel: true, BlockOnly: false, EmitWheelOnBlock: false),
            currentActive: false);

        hook.SuppressedKeyboardKeys.Should().Equal(VirtualKey.Enter);
    }

    [Fact]
    public void ApplyEnabled_ShouldKeepReservedKeys_WhenCycleRepeats()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();
        orchestrator.SetReservedPresentationKeys([VirtualKey.Enter, VirtualKey.Space]);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            orchestrator.ApplyDisabled(hook);
            orchestrator.ApplyEnabled(
                hook,
                new WpsHookInterceptDecision(InterceptKeyboard: false, InterceptWheel: true, BlockOnly: false, EmitWheelOnBlock: false),
                currentActive: false);
            hook.SuppressedKeyboardKeys.Should().Equal(VirtualKey.Enter, VirtualKey.Space);
        }
    }

    [Fact]
    public void ApplyEnabled_ShouldNotWriteReservedKeys_WhenNoReservationsConfigured()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();
        orchestrator.ApplyDisabled(hook);
        hook.SuppressedKeyboardKeys.Should().BeEmpty();

        orchestrator.ApplyEnabled(
            hook,
            new WpsHookInterceptDecision(InterceptKeyboard: false, InterceptWheel: true, BlockOnly: false, EmitWheelOnBlock: false),
            currentActive: false);

        hook.SuppressedKeyboardKeys.Should().BeEmpty();
    }

    [Fact]
    public void SetReservedPresentationKeys_ShouldClearCache_WhenCalledWithEmptyOrNull()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient();
        orchestrator.SetReservedPresentationKeys([VirtualKey.Enter]);
        orchestrator.SetReservedPresentationKeys([]);

        orchestrator.ApplyDisabled(hook);
        orchestrator.ApplyEnabled(
            hook,
            new WpsHookInterceptDecision(InterceptKeyboard: false, InterceptWheel: true, BlockOnly: false, EmitWheelOnBlock: false),
            currentActive: false);

        hook.SuppressedKeyboardKeys.Should().BeEmpty();
    }

    [Fact]
    public void ApplyDisabled_ShouldReturnDefaultState_WhenHookIsNull()
    {
        var orchestrator = new WpsHookOrchestrator();

        var state = orchestrator.ApplyDisabled(hookClient: null);

        state.IsActive.Should().BeFalse();
        state.BlockOnly.Should().BeFalse();
        state.InterceptKeyboard.Should().BeTrue();
        state.InterceptWheel.Should().BeTrue();
    }

    [Fact]
    public async Task TryStartSafeAsync_ShouldReturnFalse_WhenHookUnavailable()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient { Available = false };

        var started = await orchestrator.TryStartSafeAsync(hook);

        started.Should().BeFalse();
        hook.StartCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TryStartSafeAsync_ShouldReturnResult_WhenHookAvailable()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient { Available = true, StartResult = true };

        var started = await orchestrator.TryStartSafeAsync(hook);

        started.Should().BeTrue();
        hook.StartCalled.Should().BeTrue();
    }

    [Fact]
    public async Task TryStartSafeAsync_ShouldSwallowNonFatalAndReturnFalse()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient
        {
            Available = true,
            StartException = new InvalidOperationException("boom")
        };

        var started = await orchestrator.TryStartSafeAsync(hook);

        started.Should().BeFalse();
        hook.StartCalled.Should().BeTrue();
    }

    [Fact]
    public async Task TryStartSafeAsync_ShouldRethrowFatalException()
    {
        var orchestrator = new WpsHookOrchestrator();
        var hook = new FakeWpsNavHookClient
        {
            Available = true,
            StartException = new BadImageFormatException("fatal")
        };

        var act = async () => await orchestrator.TryStartSafeAsync(hook);

        await act.Should().ThrowAsync<BadImageFormatException>();
    }

    private sealed class FakeWpsNavHookClient : IWpsNavHookClient
    {
        public bool Available { get; set; } = true;
        public bool IsActive { get; private set; }
        public bool InterceptEnabled { get; private set; }
        public bool BlockOnly { get; private set; }
        public bool InterceptKeyboard { get; private set; } = true;
        public bool InterceptWheel { get; private set; } = true;
        public bool EmitWheelOnBlock { get; private set; } = true;
        public bool ConsumeAuthorizedInput { get; private set; }
        public IReadOnlyList<IntPtr> AuthorizedInputWindows { get; private set; } = [];
        public bool StopCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool StartResult { get; set; } = true;
        public Exception? StartException { get; set; }
        public Exception? ConfigurationException { get; set; }
        public IReadOnlyList<VirtualKey> SuppressedKeyboardKeys { get; private set; } = [VirtualKey.Enter];

        public void SetInterceptEnabled(bool enabled)
        {
            ThrowIfConfigurationRequested();
            InterceptEnabled = enabled;
        }

        public void SetBlockOnly(bool enabled)
        {
            ThrowIfConfigurationRequested();
            BlockOnly = enabled;
        }

        public void SetInterceptKeyboard(bool enabled)
        {
            ThrowIfConfigurationRequested();
            InterceptKeyboard = enabled;
        }

        public void SetInterceptWheel(bool enabled)
        {
            ThrowIfConfigurationRequested();
            InterceptWheel = enabled;
        }

        public void SetEmitWheelOnBlock(bool enabled)
        {
            ThrowIfConfigurationRequested();
            EmitWheelOnBlock = enabled;
        }

        public void SetConsumeAuthorizedInput(bool enabled)
        {
            ThrowIfConfigurationRequested();
            ConsumeAuthorizedInput = enabled;
        }

        public void SetAuthorizedInputWindows(IEnumerable<IntPtr> windows)
        {
            ThrowIfConfigurationRequested();
            AuthorizedInputWindows = windows.ToArray();
        }

        public void SetSuppressedKeyboardKeys(IEnumerable<VirtualKey> keys)
        {
            ThrowIfConfigurationRequested();
            SuppressedKeyboardKeys = keys.ToArray();
        }

        public Task<bool> StartAsync()
        {
            StartCalled = true;
            if (StartException != null)
            {
                throw StartException;
            }

            IsActive = StartResult;
            return Task.FromResult(StartResult);
        }

        public void Stop()
        {
            StopCalled = true;
            IsActive = false;
        }

        private void ThrowIfConfigurationRequested()
        {
            if (ConfigurationException is not null)
            {
                throw ConfigurationException;
            }
        }
    }
}
