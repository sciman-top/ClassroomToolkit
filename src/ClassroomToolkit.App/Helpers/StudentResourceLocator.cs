using System;
using System.IO;

namespace ClassroomToolkit.App.Helpers;

public static class StudentResourceLocator
{
    private const string WorkbookFileName = "students.xlsx";
    private const string PhotoFolderName = "student_photos";
    private const string SolutionFileName = "ClassroomToolkit.sln";

    public static string ResolveStudentWorkbookPath()
    {
        var root = ResolveResourceRoot();
        return Path.Combine(root, WorkbookFileName);
    }

    public static string ResolveStudentPhotoRoot()
    {
        var root = ResolveResourceRoot();
        var path = Path.Combine(root, PhotoFolderName);
        TryEnsureDirectory(path);
        return path;
    }

    private static string ResolveResourceRoot()
    {
        var solutionDir = FindSolutionDirectory();
        if (!string.IsNullOrWhiteSpace(solutionDir))
        {
            return solutionDir;
        }
        return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
    }

    private static string? FindSolutionDirectory()
    {
        var starts = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Environment.CurrentDirectory
        };
        foreach (var start in starts)
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current != null)
            {
                var slnPath = Path.Combine(current.FullName, SolutionFileName);
                if (File.Exists(slnPath))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
        }
        return null;
    }

    private static void TryEnsureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(path);
        }
        catch
        {
            return;
        }
    }

}
