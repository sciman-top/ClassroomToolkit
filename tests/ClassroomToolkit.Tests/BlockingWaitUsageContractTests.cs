using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BlockingWaitUsageContractTests
{
    private static readonly HashSet<string> AllowedBlockingUsage = new(StringComparer.Ordinal);

    [Fact]
    public void Source_ShouldNotIntroduceNewBlockingWaitPatterns()
    {
        var sourceRoot = TestPathHelper.ResolveRepoPath("src");
        var offenders = new List<string>();
        var discoveredAllowed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!ContainsBlockingWaitPattern(line))
                {
                    continue;
                }

                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var relativePath = TestPathHelper.GetRelativeRepoPath(file).Replace('\\', '/');
                var signature = $"{relativePath}|{trimmed}";
                if (AllowedBlockingUsage.Contains(signature))
                {
                    discoveredAllowed.Add(signature);
                    continue;
                }

                offenders.Add($"{relativePath}:{index + 1}:{trimmed}");
            }
        }

        offenders.Should().BeEmpty("production code should avoid new blocking waits to prevent deadlock and UI stalls");
        var missingAllowListEntries = AllowedBlockingUsage.Except(discoveredAllowed).ToArray();
        missingAllowListEntries.Should().BeEmpty("allow-list should track current legacy blocking wait usage exactly");
    }

    private static bool ContainsBlockingWaitPattern(string line)
    {
        return line.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal)
               || line.Contains(".Wait(", StringComparison.Ordinal);
    }
}
