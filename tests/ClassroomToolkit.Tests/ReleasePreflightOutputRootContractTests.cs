using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ReleasePreflightOutputRootContractTests
{
    [Fact]
    public void PreflightCheck_ShouldRespectRootedOutputRoot()
    {
        var scriptPath = TestPathHelper.ResolveRepoPath("scripts", "release", "preflight-check.ps1");
        File.Exists(scriptPath).Should().BeTrue();

        var source = File.ReadAllText(scriptPath);

        source.Should().Contain("[System.IO.Path]::IsPathRooted($OutputRoot)");
        source.Should().Contain("Join-Path $repoRoot $OutputRoot");
        source.Should().NotContain("ForEach-Object { Join-Path $_.Path $OutputRoot }");
    }
}
