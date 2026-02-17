using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassroomToolkit.App.Ink;

public sealed class InkStorageService
{
    private const string DefaultRootPath = @"D:\ClassroomToolkit\Ink";
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
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }
        var jsonPath = GetPageJsonPath(date, page.DocumentName, page.PageIndex);
        var json = JsonSerializer.Serialize(page, _options);
        WriteAllTextAtomically(jsonPath, json);
    }

    public InkPageData? LoadPage(DateTime date, string documentName, int pageIndex)
    {
        var jsonPath = GetPageJsonPath(date, documentName, pageIndex);
        if (!File.Exists(jsonPath))
        {
            return null;
        }
        try
        {
            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<InkPageData>(json, _options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public IReadOnlyList<DateTime> ListDates()
    {
        if (!Directory.Exists(_rootPath))
        {
            return Array.Empty<DateTime>();
        }
        var folders = Directory.GetDirectories(_rootPath);
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

    public IReadOnlyList<string> ListDocuments(DateTime date)
    {
        var dateFolder = Path.Combine(_rootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        if (!Directory.Exists(dateFolder))
        {
            return Array.Empty<string>();
        }
        return Directory.GetDirectories(dateFolder)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        foreach (var file in Directory.GetFiles(pagesFolder, "slide_*.json"))
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
            catch (IOException)
            {
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
        foreach (var folder in Directory.GetDirectories(rootPath))
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
            catch
            {
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
        return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
    }

    private static void WriteAllTextAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, content);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
