using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintModeManagerEventCallbackSafetyContractTests
{
    [Fact]
    public void StateChangedCallbacks_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(() => PaintModeChanged?.Invoke(value))");
        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(() => IsDrawingChanged?.Invoke(value))");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintModeManager.cs");
    }
}
