using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class GlobalHookServiceLifecycleContractTests
{
    [Fact]
    public void GlobalHookService_ShouldDispatchHookUnavailable_ByInvocationList()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("HookUnavailable?.GetInvocationList()");
        source.Should().Contain("foreach (var callback in handlers)");
    }

    [Fact]
    public void GlobalHookService_ShouldIsolateRecoverableHookUnavailableCallbackFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("catch (Exception ex) when (IsNonFatal(ex))");
        source.Should().Contain("HookUnavailable callback failed");
    }

    [Fact]
    public void GlobalHookService_ShouldCleanupStartedHooks_WhenStartThrowsRecoverableException()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("Start hook failed");
        source.Should().Contain("CleanupHooks(startedHooks, callback);");
        source.Should().Contain("NotifyHookUnavailable();");
    }

    [Fact]
    public void GlobalHookService_ShouldDisposeHookInstances_WhenStopping()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("hook.Dispose();");
    }

    [Fact]
    public void GlobalHookService_ShouldRollbackAllStartedHooks_WhenAnyHookIsInactive()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("register-inactive");
        source.Should().Contain("CleanupHooks(startedHooks, callback);");
        source.Should().Contain("NotifyHookUnavailable();");
        source.Should().Contain("return false;");
    }

    [Fact]
    public void GlobalHookService_ShouldCleanupStartedHooks_WhenBindingEnumerationThrowsRecoverableException()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("Register bindings failed");
        source.Should().Contain("CleanupHooks(startedHooks, callback);");
        source.Should().Contain("NotifyHookUnavailable();");
    }

    [Fact]
    public void GlobalHookService_ShouldIsolateRecoverableBindingCallbackFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("TryInvokeBindingCallback(callback)");
        source.Should().Contain("private static void TryInvokeBindingCallback(Action callback)");
        source.Should().Contain("Binding callback failed");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Services",
            "Input",
            "GlobalHookService.cs");
    }
}
