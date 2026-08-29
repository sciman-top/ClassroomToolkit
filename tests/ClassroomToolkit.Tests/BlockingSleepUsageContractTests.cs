using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BlockingSleepUsageContractTests
{
    private static readonly HashSet<string> AllowedSleepUsage = new(StringComparer.Ordinal)
    {
        // AtomicFileReplaceUtility 在同步保存路径上对“目标文件被杀软/索引器短暂占用”做
        // 有界重试（5 次 × 50ms）。这是唯一豁免的阻塞等待：替代方案要么把整条同步保存链
        // 异步化，要么在 UI 线程忙等，代价更高。新增豁免必须在此登记并说明理由。
        "src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs|Thread.Sleep(TransientReplaceRetryDelayMilliseconds);",
    };

    [Fact]
    public void Source_ShouldNotContainThreadSleep()
    {
        var sourceRoot = TestPathHelper.ResolveRepoPath("src");
        var offenders = new List<string>();
        var discoveredAllowed = new HashSet<string>(StringComparer.Ordinal);

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
                var signature = $"{relativePath}|{lines[index].Trim()}";
                if (AllowedSleepUsage.Contains(signature))
                {
                    discoveredAllowed.Add(signature);
                    continue;
                }

                offenders.Add($"{relativePath}:{index + 1}");
            }
        }

        offenders.Should().BeEmpty("production code should avoid blocking sleeps to prevent UI stalls");
        var missingAllowListEntries = AllowedSleepUsage.Except(discoveredAllowed).ToArray();
        missingAllowListEntries.Should().BeEmpty("allow-list should track current sanctioned sleep usage exactly");
    }
}
