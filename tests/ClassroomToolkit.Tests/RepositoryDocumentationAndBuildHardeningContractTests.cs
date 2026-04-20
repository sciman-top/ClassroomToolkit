using System.Text.RegularExpressions;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RepositoryDocumentationAndBuildHardeningContractTests
{
    [Theory]
    [InlineData("README.md")]
    [InlineData("README.en.md")]
    public void Readme_LocalMarkdownLinks_ShouldPointToExistingPaths(string readmePath)
    {
        var absoluteReadmePath = TestPathHelper.ResolveRepoPath(readmePath);
        var content = File.ReadAllText(absoluteReadmePath);

        var missingTargets = Regex.Matches(content, @"\]\((\./[^)#]+)\)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(relativePath =>
            {
                var normalized = relativePath[2..].Replace('/', Path.DirectorySeparatorChar);
                var targetPath = TestPathHelper.ResolveRepoPath(normalized);
                return !File.Exists(targetPath) && !Directory.Exists(targetPath);
            })
            .ToArray();

        missingTargets.Should().BeEmpty();
    }

    [Theory]
    [InlineData("README.md", "scripts/doctor.ps1")]
    [InlineData("README.en.md", "tools/browser-session/start-browser-session.ps1")]
    public void Readme_CommandExamples_ShouldNotReferenceMissingRepoScripts(string readmePath, string missingScriptPath)
    {
        var absoluteReadmePath = TestPathHelper.ResolveRepoPath(readmePath);
        var content = File.ReadAllText(absoluteReadmePath);

        content.Should().NotContain(missingScriptPath);
    }

    [Fact]
    public void DirectoryBuildProps_ShouldEnableCentralBuildHardening()
    {
        var propsPath = TestPathHelper.ResolveRepoPath("Directory.Build.props");

        File.Exists(propsPath).Should().BeTrue();

        var content = File.ReadAllText(propsPath);
        content.Should().Contain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
    }
}
