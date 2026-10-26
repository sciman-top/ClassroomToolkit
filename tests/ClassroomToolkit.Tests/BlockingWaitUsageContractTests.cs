using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BlockingWaitUsageContractTests
{
    // 关闭路径在 bump generation 作废排队任务后，同步等待唯一在途后台写盘完成，
    // 避免两进程内并发原子替换同一 students.xlsx；有界（单次写盘周期）且仅限用户主动关闭。
    // 回收条件：RollCallWindow 关闭路径改为异步保存编排后移除本条目。
    private static readonly HashSet<string> AllowedBlockingUsage = new(StringComparer.Ordinal)
    {
        "src/ClassroomToolkit.App/RollCall/RollCallViewModel.Navigation.cs|_workbookSaveGate.Wait();"
    };

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
               || line.Contains(".Wait(", StringComparison.Ordinal)
               || line.Contains(".Result", StringComparison.Ordinal);
    }
}
