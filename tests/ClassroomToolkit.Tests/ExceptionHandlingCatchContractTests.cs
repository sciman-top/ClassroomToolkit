using System.Text.RegularExpressions;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ExceptionHandlingCatchContractTests
{
    private static readonly Regex BareCatchPattern = new(@"^\s*catch\s*$", RegexOptions.Compiled);
    private static readonly Regex CatchExceptionWithoutFilterPattern = new(
        @"^\s*catch\s*\(\s*Exception(?:\s+\w+)?\s*\)(?!\s*when)",
        RegexOptions.Compiled);

    [Fact]
    public void Source_ShouldNotContainBareCatch()
    {
        var offenders = FindOffenders(BareCatchPattern);

        offenders.Should().BeEmpty(
            "all catch blocks should explicitly encode fatal/non-fatal policy");
    }

    [Fact]
    public void Source_ShouldNotContainCatchExceptionWithoutFilter()
    {
        var offenders = FindOffenders(CatchExceptionWithoutFilterPattern);

        offenders.Should().BeEmpty(
            "catch (Exception) must be guarded by a policy filter (for example: when (Policy.IsNonFatal(ex)))");
    }

    private static List<string> FindOffenders(Regex pattern)
    {
        var sourceRoot = TestPathHelper.ResolveRepoPath("src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!pattern.IsMatch(lines[index]))
                {
                    continue;
                }

                var relativePath = TestPathHelper.GetRelativeRepoPath(file).Replace('\\', '/');
                offenders.Add($"{relativePath}:{index + 1}");
            }
        }

        return offenders;
    }
}
