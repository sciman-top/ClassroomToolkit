using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

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
        public bool InterceptEnabled { get; private set; }
        public bool BlockOnly { get; private set; }
        public bool InterceptKeyboard { get; private set; } = true;
        public bool InterceptWheel { get; private set; } = true;
        public bool EmitWheelOnBlock { get; private set; } = true;
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

            return Task.FromResult(StartResult);
        }

        public void Stop()
        {
            StopCalled = true;
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
