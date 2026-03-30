using System;
using System.IO;

namespace ClassroomToolkit.Tests;

internal static class TestPathHelper
{
    public static string GetRepositoryRootOrThrow()
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        var repoRoot = FindRepositoryRoot(baseDir)?.FullName;
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new DirectoryNotFoundException("Cannot locate repository root from test base directory.");
        }

        return repoRoot;
    }

    public static string CreateDirectory(string prefix)
    {
        var root = GetWritableRoot();
        var path = Path.Combine(root, $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string CreateFilePath(string prefix, string extension)
    {
        var root = GetWritableRoot();
        return Path.Combine(root, $"{prefix}_{Guid.NewGuid():N}{extension}");
    }

    public static string CreateIsolatedDirectory(string prefix)
    {
        var root = GetIsolatedWritableRoot();
        var path = Path.Combine(root, $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ResolveRepoPath(params string[] segments)
    {
        var root = GetRepositoryRootOrThrow();
        if (segments is null || segments.Length == 0)
        {
            return root;
        }

        var fullPath = new string[segments.Length + 1];
        fullPath[0] = root;
        Array.Copy(segments, 0, fullPath, 1, segments.Length);
        return Path.Combine(fullPath);
    }

    public static string ResolveAppPath(params string[] segments)
    {
        if (segments is null || segments.Length == 0)
        {
            return ResolveRepoPath("src", "ClassroomToolkit.App");
        }

        var fullSegments = new string[segments.Length + 2];
        fullSegments[0] = "src";
        fullSegments[1] = "ClassroomToolkit.App";
        Array.Copy(segments, 0, fullSegments, 2, segments.Length);
        return ResolveRepoPath(fullSegments);
    }

    public static string GetRelativeRepoPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        return Path.GetRelativePath(GetRepositoryRootOrThrow(), path);
    }

    private static string GetWritableRoot()
    {
        var repoRoot = GetRepositoryRootOrThrow();
        var root = Path.Combine(repoRoot, "tests", ".tmp");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetIsolatedWritableRoot()
    {
        try
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userHome))
            {
                var root = Path.Combine(userHome, ".codex", "memories", "classroomtoolkit-tests");
                Directory.CreateDirectory(root);
                return root;
            }
        }
        catch
        {
            // fall back to repository-local writable root
        }

        return GetWritableRoot();
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            var sln = Path.Combine(current.FullName, "ClassroomToolkit.sln");
            var git = Path.Combine(current.FullName, ".git");
            if (File.Exists(sln) || Directory.Exists(git))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
