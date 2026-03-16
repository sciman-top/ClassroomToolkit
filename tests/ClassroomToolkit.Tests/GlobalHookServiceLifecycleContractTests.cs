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

    private static string GetSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.Services",
            "Input",
            "GlobalHookService.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
