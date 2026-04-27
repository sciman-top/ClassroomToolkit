using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.App;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.App.Ink;

public sealed class InkStorageService
{
    private static readonly string DefaultRootPath = ResolveDefaultRootPath();
    private static readonly EnumerationOptions TopLevelIgnoreInaccessibleOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true
    };
    private const string PagesFolderName = "pages";
    private const string DefaultPhotosFolderName = "Photos";

    private readonly string _rootPath;
    private readonly string _photoRootPath;

    private readonly JsonSerializerOptions _options;

    public InkStorageService(string? rootPath = null, string? photoRootPath = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? DefaultRootPath : rootPath;
        var photoRoot = string.IsNullOrWhiteSpace(photoRootPath)
            ? Path.Combine(_rootPath, DefaultPhotosFolderName)
            : photoRootPath;
        _photoRootPath = photoRoot;
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    public string EnsureRoot()
    {
        Directory.CreateDirectory(_rootPath);
        return _rootPath;
    }

    public string EnsureDocumentFolder(DateTime date, string documentName)
    {
        var dateFolder = EnsureDateFolder(date);
        var safeName = SanitizeName(documentName);
        var docFolder = Path.Combine(dateFolder, safeName);
        Directory.CreateDirectory(Path.Combine(docFolder, PagesFolderName));
        return docFolder;
    }

    public string EnsureDateFolder(DateTime date)
    {
        var dateFolder = Path.Combine(_rootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dateFolder);
        return dateFolder;
    }

    public string GetPageJsonPath(DateTime date, string documentName, int pageIndex)
    {
        var docFolder = EnsureDocumentFolder(date, documentName);
        var fileName = $"slide_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}.json";
        return Path.Combine(docFolder, PagesFolderName, fileName);
    }

    public string GetPageImagePath(DateTime date, string documentName, int pageIndex)
    {
        var docFolder = EnsureDocumentFolder(date, documentName);
        var fileName = $"slide_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}.png";
        return Path.Combine(docFolder, PagesFolderName, fileName);
    }

    public void SavePage(DateTime date, InkPageData page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var jsonPath = GetPageJsonPath(date, page.DocumentName, page.PageIndex);
        var json = JsonSerializer.Serialize(page, _options);
        WriteAllTextAtomically(jsonPath, json);
    }

    public InkPageData? LoadPage(DateTime date, string documentName, int pageIndex)
    {
        string? jsonPath = null;
        try
        {
            jsonPath = GetPageJsonPath(date, documentName, pageIndex);
            if (!File.Exists(jsonPath))
            {
                return null;
            }

            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<InkPageData>(json, _options);
        }
        catch (JsonException)
        {
            Debug.WriteLine($"[InkStorage] failed to parse page json path={jsonPath ?? "(unresolved)"} document={documentName} page={pageIndex}");
            return null;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkStorage] failed to read page json path={jsonPath ?? "(unresolved)"} document={documentName} page={pageIndex} ex={ex.GetType().Name} msg={ex.Message}");
            return null;
        }
    }

    public IReadOnlyList<DateTime> ListDates()
    {
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return Array.Empty<DateTime>();
            }
            var folders = Directory.EnumerateDirectories(_rootPath, "*", TopLevelIgnoreInaccessibleOptions);
            var dates = new List<DateTime>();
            foreach (var folder in folders)
            {
                var name = Path.GetFileName(folder);
                if (DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    dates.Add(date);
                }
            }
            dates.Sort();
            return dates;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return Array.Empty<DateTime>();
        }
    }

    public IReadOnlyList<string> ListDocuments(DateTime date)
    {
        try
        {
            var dateFolder = Path.Combine(_rootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            if (!Directory.Exists(dateFolder))
            {
                return Array.Empty<string>();
            }
            return Directory.EnumerateDirectories(dateFolder, "*", TopLevelIgnoreInaccessibleOptions)
                .Select(path => Path.GetFileName(path) ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<InkPageData> ListPages(DateTime date, string documentName)
    {
        var safeName = SanitizeName(documentName);
        var docFolder = Path.Combine(_rootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), safeName);
        var pagesFolder = Path.Combine(docFolder, PagesFolderName);
        if (!Directory.Exists(pagesFolder))
        {
            return Array.Empty<InkPageData>();
        }
        var result = new List<InkPageData>();
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(pagesFolder, "slide_*.json", TopLevelIgnoreInaccessibleOptions);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return Array.Empty<InkPageData>();
        }
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var page = JsonSerializer.Deserialize<InkPageData>(json, _options);
                if (page != null)
                {
                    result.Add(page);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed page files and keep loading other pages.
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine($"[InkStorage] failed to read list page json path={file} ex={ex.GetType().Name} msg={ex.Message}");
                // Ignore transient IO failures on individual files.
            }
        }
        return result.OrderBy(page => page.PageIndex).ToList();
    }

    public string CopyPhoto(string sourcePath, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Invalid photo path.", nameof(sourcePath));
        }
        EnsureRoot();
        var dateFolder = Path.Combine(_photoRootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dateFolder);
        var fileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Guid.NewGuid().ToString("N");
        }
        var targetPath = Path.Combine(dateFolder, fileName);
        if (File.Exists(targetPath))
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            for (var i = 1; i < 1000; i++)
            {
                var candidate = Path.Combine(dateFolder, $"{name}_{i}{ext}");
                if (!File.Exists(candidate))
                {
                    targetPath = candidate;
                    break;
                }
            }
        }
        File.Copy(sourcePath, targetPath, overwrite: false);
        return targetPath;
    }

    public DateTime? FindLatestDateWithDocument(string documentName, DateTime maxDate)
    {
        if (string.IsNullOrWhiteSpace(documentName))
        {
            return null;
        }
        var safeName = SanitizeName(documentName);
        var dates = ListDates()
            .Where(date => date.Date <= maxDate.Date)
            .OrderByDescending(date => date)
            .ToList();
        foreach (var date in dates)
        {
            var dateFolder = Path.Combine(_rootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            var docFolder = Path.Combine(dateFolder, safeName);
            if (Directory.Exists(docFolder))
            {
                return date;
            }
        }
        return null;
    }

    public void CleanupOldRecords(int retentionDays)
    {
        if (retentionDays <= 0)
        {
            return;
        }
        var cutoff = DateTime.Today.AddDays(-retentionDays);
        CleanupDateFolders(_rootPath, cutoff);
        CleanupDateFolders(_photoRootPath, cutoff);
    }

    private static void CleanupDateFolders(string rootPath, DateTime cutoff)
    {
        if (!Directory.Exists(rootPath))
        {
            return;
        }
        foreach (var folder in Directory.EnumerateDirectories(rootPath, "*", TopLevelIgnoreInaccessibleOptions))
        {
            var name = Path.GetFileName(folder);
            if (!DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }
            if (date.Date >= cutoff.Date)
            {
                continue;
            }
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine($"[InkStorage] cleanup folder failed path={folder} ex={ex.GetType().Name} msg={ex.Message}");
                // Ignore cleanup failures.
            }
        }
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "unknown";
        }
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Where(ch => !invalid.Contains(ch)).ToArray());
        if (safe is "." or "..")
        {
            return "unknown";
        }

        return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
    }

    private static void WriteAllTextAtomically(string path, string content)
    {
        AtomicFileReplaceUtility.WriteAtomically(
            path,
            tempPath => File.WriteAllText(tempPath, content),
            onTempCleanupFailure: static (_, ex) =>
            {
                if (!AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    return;
                }

                // Best-effort cleanup; keep the primary write/replace exception.
            });
    }

    private static string ResolveDefaultRootPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            return Path.Combine(local, "ClassroomToolkit", "Ink");
        }

        return Path.Combine(AppContext.BaseDirectory, "Ink");
    }
}
