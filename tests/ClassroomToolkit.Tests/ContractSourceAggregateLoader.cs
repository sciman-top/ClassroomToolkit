using System;
using System.IO;
using System.Linq;

namespace ClassroomToolkit.Tests;

internal static class ContractSourceAggregateLoader
{
    internal static string LoadByPattern(params string[] pathAndPattern)
    {
        if (pathAndPattern == null || pathAndPattern.Length < 2)
        {
            throw new ArgumentException("Path and pattern are required.", nameof(pathAndPattern));
        }

        var pattern = pathAndPattern[^1];
        var pathSegments = pathAndPattern[..^1];
        var directory = TestPathHelper.ResolveRepoPath(pathSegments);

        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
    }

    internal static int CountOccurrences(string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        var offset = 0;

        while (offset < source.Length)
        {
            var index = source.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            offset = index + value.Length;
        }

        return count;
    }
}
