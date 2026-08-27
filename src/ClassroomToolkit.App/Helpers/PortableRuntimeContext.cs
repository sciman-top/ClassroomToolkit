using System;
using System.IO;

namespace ClassroomToolkit.App.Helpers;

internal static class PortableRuntimeContext
{
    internal const string MarkerFileName = "portable.mode";
    internal const string ReleaseMetadataFileName = "portable-release.json";
    internal const string DataFolderName = "data";

    public static bool IsEnabled => TryResolveRoot(AppContext.BaseDirectory, out _);

    public static string? RootDirectory => TryResolveRoot(AppContext.BaseDirectory, out var root)
        ? root
        : null;

    public static string? DataDirectory => RootDirectory is { } root
        ? Path.Combine(root, DataFolderName)
        : null;

    internal static bool TryResolveRoot(string? baseDirectory, out string root)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return false;
        }

        try
        {
            var current = new DirectoryInfo(Path.GetFullPath(baseDirectory));
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, MarkerFileName)))
                {
                    root = current.FullName;
                    return true;
                }

                current = current.Parent;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }
}
