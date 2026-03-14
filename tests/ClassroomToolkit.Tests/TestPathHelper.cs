using System;
using System.IO;

namespace ClassroomToolkit.Tests;

internal static class TestPathHelper
{
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

    private static string GetWritableRoot()
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        var repoRoot = FindRepositoryRoot(baseDir)?.FullName ?? AppContext.BaseDirectory;
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
