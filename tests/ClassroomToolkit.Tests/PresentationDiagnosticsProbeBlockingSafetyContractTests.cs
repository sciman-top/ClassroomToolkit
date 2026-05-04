using System.IO;
using System.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationDiagnosticsProbeBlockingSafetyContractTests
{
    [Fact]
    public void Probe_ShouldUseBoundedWaitForHookStartup()
    {
        var source = File.ReadAllText(GetSourcePath());
        var compactSource = string.Concat(source.Where(c => !char.IsWhiteSpace(c)));

        source.Should().Contain("HookStartWaitTimeoutMs = 2000");
        source.Should().Contain("TryWaitTask(hook.StartAsync(), HookStartWaitTimeoutMs");
        compactSource.Should().Contain(".WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false).GetAwaiter().GetResult()");
        source.Should().NotContain(".Wait(");
        source.Should().NotContain(".Result");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Services",
            "Presentation",
            "PresentationDiagnosticsProbe.cs");
    }
}
