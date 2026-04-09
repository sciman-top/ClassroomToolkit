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
}
