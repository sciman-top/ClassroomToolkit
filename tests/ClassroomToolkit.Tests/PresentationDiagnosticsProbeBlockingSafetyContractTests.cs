using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationDiagnosticsProbeBlockingSafetyContractTests
{
    [Fact]
    public void Probe_ShouldUseBoundedWaitAndAvoidUnboundedGetResult()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("HookStartWaitTimeoutMs = 2000");
        source.Should().Contain("TryWaitTask(hook.StartAsync(), HookStartWaitTimeoutMs");
        source.Should().NotContain(".GetAwaiter().GetResult()");
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
