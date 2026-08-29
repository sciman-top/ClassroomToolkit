using System;
using System.IO;

namespace ClassroomToolkit.App.Helpers;

internal static class StudentResourceLocator
{
    private const string WorkbookFileName = "students.xlsx";
    private const string PhotoFolderName = "student_photos";
    private const string DefaultPhotoClassFolderName = "1班";
    private const string SolutionFileName = "ClassroomToolkit.sln";
    private const string AppDataFolderName = "ClassroomToolkit";
    private const string DataFolderName = "data";

    public static string ResolveStudentWorkbookPath()
    {
        var root = ResolveResourceRoot();
        TryEnsureDirectory(root);
        return Path.Combine(root, WorkbookFileName);
    }

    public static string ResolveStudentPhotoRoot()
    {
        var root = ResolveResourceRoot();
        var path = Path.Combine(root, PhotoFolderName);
        TryEnsureDirectory(path);
        TryEnsureDirectory(Path.Combine(path, DefaultPhotoClassFolderName));
        return path;
    }

    private static string ResolveResourceRoot()
    {
        if (PortableRuntimeContext.DataDirectory is { } portableDataRoot)
        {
            return portableDataRoot;
        }

        var baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        var solutionDir = FindSolutionDirectory(baseDirectory);
        if (!string.IsNullOrWhiteSpace(solutionDir))
        {
            return ResolveDevelopmentDataRoot(solutionDir);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return baseDirectory;
        }

        var persistentRoot = Path.Combine(localAppData, AppDataFolderName, DataFolderName);
        TryCopyLegacyClassroomData(baseDirectory, persistentRoot);
        return persistentRoot;
    }

    // Development runs share the deployed data/ layout; legacy solution-root files are copied in on first run.
    internal static string ResolveDevelopmentDataRoot(string solutionDir)
    {
        var devDataRoot = Path.Combine(solutionDir, DataFolderName);
        TryCopyLegacyClassroomData(solutionDir, devDataRoot);
        return devDataRoot;
    }

    // Package updates replace application directories, so classroom data must live outside them.
    private static void TryCopyLegacyClassroomData(string legacyRoot, string targetRoot)
    {
        try
        {
            if (string.Equals(
                legacyRoot.TrimEnd(Path.DirectorySeparatorChar),
                targetRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var legacyWorkbook = Path.Combine(legacyRoot, WorkbookFileName);
            var persistentWorkbook = Path.Combine(targetRoot, WorkbookFileName);
            if (File.Exists(legacyWorkbook) && !File.Exists(persistentWorkbook))
            {
                Directory.CreateDirectory(targetRoot);
                var pendingWorkbook = persistentWorkbook + ".migration-pending";
                File.Copy(legacyWorkbook, pendingWorkbook, overwrite: true);
                File.Move(pendingWorkbook, persistentWorkbook);
            }

            var legacyPhotos = Path.Combine(legacyRoot, PhotoFolderName);
            var persistentPhotos = Path.Combine(targetRoot, PhotoFolderName);
            if (Directory.Exists(legacyPhotos) && !Directory.Exists(persistentPhotos))
            {
                CopyDirectory(legacyPhotos, persistentPhotos);
            }
        }
        catch (IOException)
        {
            // Preserve legacy data in place when the first-run copy cannot complete.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve legacy data in place when the target cannot be written.
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    internal static string? FindSolutionDirectory(params string?[] starts)
    {
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            DirectoryInfo? current;
            try
            {
                current = new DirectoryInfo(Path.GetFullPath(start));
            }
            catch (ArgumentException)
            {
                continue;
            }
            catch (NotSupportedException)
            {
                continue;
            }
            catch (PathTooLongException)
            {
                continue;
            }

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
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }

}
