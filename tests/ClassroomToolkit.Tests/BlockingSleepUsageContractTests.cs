using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BlockingSleepUsageContractTests
{
    [Fact]
    public void Source_ShouldNotContainThreadSleep()
    {
        var sourceRoot = TestPathHelper.ResolveRepoPath("src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains("Thread.Sleep(", StringComparison.Ordinal))
                {
                    continue;
                }

                var relativePath = TestPathHelper.GetRelativeRepoPath(file).Replace('\\', '/');
                offenders.Add($"{relativePath}:{index + 1}");
            }
        }

        offenders.Should().BeEmpty("production code should avoid blocking sleeps to prevent UI stalls");
    }
}

