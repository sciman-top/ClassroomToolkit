using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class GovernanceTruthSourceContractTests
{
    [Fact]
    public void CanonicalGovernanceEntrypoints_ShouldExist()
    {
        File.Exists(TestPathHelper.ResolveRepoPath("scripts", "quality", "run-local-quality-gates.ps1")).Should().BeTrue();
        File.Exists(TestPathHelper.ResolveRepoPath("scripts", "quality", "check-governance-truth-source.ps1")).Should().BeTrue();
        File.Exists(TestPathHelper.ResolveRepoPath("scripts", "quality", "check-analyzer-backlog-baseline.ps1")).Should().BeTrue();
        File.Exists(TestPathHelper.ResolveRepoPath("scripts", "quality", "analyzer-backlog-baseline.json")).Should().BeTrue();
        File.Exists(TestPathHelper.ResolveRepoPath("azure-pipelines.yml")).Should().BeTrue();
        File.Exists(TestPathHelper.ResolveRepoPath(".gitlab-ci.yml")).Should().BeTrue();
    }

    [Fact]
    public void RetiredGovernancePaths_ShouldRemainAbsent()
    {
        Directory.Exists(TestPathHelper.ResolveRepoPath("scripts", "governance")).Should().BeFalse();
        File.Exists(TestPathHelper.ResolveRepoPath(".github", "workflows", "quality-gate.yml")).Should().BeFalse();
        File.Exists(TestPathHelper.ResolveRepoPath(".github", "workflows", "quality-gates.yml")).Should().BeFalse();
    }

    [Fact]
    public void ActiveDocs_ShouldNotReferenceRetiredGovernancePaths()
    {
        var blockedTokens = new[]
        {
            "scripts/governance/",
            ".github/workflows/quality-gate.yml",
            ".github/workflows/quality-gates.yml"
        };

        var activeDocs = new[]
        {
            "README.md",
            "README.en.md",
            "docs/handover.md",
            "docs/runbooks/governance-endstate-maintenance.md"
        };

        foreach (var doc in activeDocs)
        {
            var fullPath = TestPathHelper.ResolveRepoPath(
                doc.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            File.Exists(fullPath).Should().BeTrue();

            var content = File.ReadAllText(fullPath);
            foreach (var token in blockedTokens)
            {
                content.Should().NotContain(
                    token,
                    $"{doc} should align with the active governance truth source");
            }
        }
    }

    [Fact]
    public void LocalQualityGate_ShouldInvokeGovernanceTruthSourceCheck()
    {
        var scriptPath = TestPathHelper.ResolveRepoPath("scripts", "quality", "run-local-quality-gates.ps1");
        File.Exists(scriptPath).Should().BeTrue();

        var content = File.ReadAllText(scriptPath);
        content.Should().Contain("check-governance-truth-source.ps1");
        content.Should().Contain("check-analyzer-backlog-baseline.ps1");
    }
}
