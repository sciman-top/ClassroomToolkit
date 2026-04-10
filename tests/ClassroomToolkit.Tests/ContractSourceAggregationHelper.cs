using System.IO;
using System.Linq;

namespace ClassroomToolkit.Tests;

internal static class ContractSourceAggregationHelper
{
    internal static string ReadSourcesInDirectory(
        string[] directorySegments,
        string pattern)
    {
        var directory = TestPathHelper.ResolveRepoPath(directorySegments);
        var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path)
            .ToArray();

        return string.Join("\n", files.Select(File.ReadAllText));
    }
}
