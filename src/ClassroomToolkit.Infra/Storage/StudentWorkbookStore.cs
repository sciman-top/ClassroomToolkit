using ClosedXML.Excel;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;
using System.Collections.Concurrent;
using System.Security.Cryptography;

using ClassroomToolkit.Infra.Logging;

namespace ClassroomToolkit.Infra.Storage;

public sealed record StudentWorkbookLoadResult(
    StudentWorkbook Workbook,
    bool CreatedTemplate,
    string? RollStateJson,
    bool OverwriteBlocked = false);

/// <summary>工作簿拒绝覆盖（此前读取失败或加载后被外部修改）；SQLite 降级链路据此保留快照出路。</summary>
public sealed class StudentWorkbookOverwriteRefusedException : InvalidOperationException
{
    public StudentWorkbookOverwriteRefusedException()
    {
    }

    public StudentWorkbookOverwriteRefusedException(string message)
        : base(message)
    {
    }

    public StudentWorkbookOverwriteRefusedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

public sealed class StudentWorkbookStore
{
    public const string DefaultClassName = "1班";
    public const string RollStateSheetName = "_ROLL_STATE";
    public const string RollStateColumn = "ROLL_STATE_JSON";

    private readonly ConcurrentDictionary<string, byte> _overwriteBlockedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ValidatedFileState> _lastValidatedFileStates = new(StringComparer.OrdinalIgnoreCase);

    private sealed record ValidatedFileState(long WriteTimeUtcTicks, string ContentHash);

    public StudentWorkbookLoadResult LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            _overwriteBlockedPaths.TryRemove(fullPath, out _);
            var template = StudentWorkbookSpreadsheetCodec.CreateTemplateWorkbook();
            Save(template.Workbook, fullPath, template.RollStateJson);
            return template with { CreatedTemplate = true };
        }

        try
        {
            var result = LoadExistingWorkbook(fullPath);
            if (result.OverwriteBlocked)
            {
                _overwriteBlockedPaths[fullPath] = 0;
            }
            else
            {
                _overwriteBlockedPaths.TryRemove(fullPath, out _);
            }

            return result;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            _overwriteBlockedPaths[fullPath] = 0;
            throw;
        }
    }

    private StudentWorkbookLoadResult LoadExistingWorkbook(string path)
    {
        using var workbook = new XLWorkbook(path);
        // Capture the fingerprint after opening the exact source and before parsing or normalization.
        RecordFileState(path);
        var parsed = StudentWorkbookSpreadsheetCodec.Read(workbook);
        var overwriteBlocked = false;
        if (parsed.NeedsRepair)
        {
            if (TryEnsureNormalizationBackup(path))
            {
                _overwriteBlockedPaths.TryRemove(path, out _);
                Save(parsed.Workbook, path, parsed.RollStateJson);
            }
            else
            {
                overwriteBlocked = true;
                InfraDiagnosticsLog.Write(
                    $"[StudentWorkbookStore] normalization backup failed; degrade to read-only session path={path}");
            }
        }

        return new StudentWorkbookLoadResult(parsed.Workbook, false, parsed.RollStateJson, overwriteBlocked);
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && _overwriteBlockedPaths.ContainsKey(fullPath))
        {
            throw new StudentWorkbookOverwriteRefusedException(
                $"学生工作簿此前读取失败；拒绝覆盖原文件，需先恢复或替换后重新加载：{fullPath}");
        }
        EnsureNoExternalModification(fullPath);

        var extension = System.IO.Path.GetExtension(fullPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".xlsx";
        }

        AtomicFileReplaceUtility.WriteAtomically(
            fullPath,
            extension,
            tempPath =>
            {
                using var xl = new XLWorkbook();
                StudentWorkbookSpreadsheetCodec.Write(xl, workbook, rollStateJson);
                xl.SaveAs(tempPath);
            },
            onTempCleanupFailure: static (tempPath, ex) =>
            {
                InfraDiagnosticsLog.Write(
                    $"[StudentWorkbookStore] temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
        _overwriteBlockedPaths.TryRemove(fullPath, out _);
        RecordFileState(fullPath);
    }

    /// <summary>
    /// 写前外部变更检测：加载后若文件被外部（如 Excel）修改，拒绝用内存旧快照整册覆盖。
    /// 即便 mtime 相同也核对内容，防止保留时间戳的复制/恢复绕过保护；
    /// 无法读取当前内容时传播 IO 错误，保留原文件和调用方待保存状态。
    /// </summary>
    private void EnsureNoExternalModification(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            _lastValidatedFileStates.TryRemove(fullPath, out _);
            return;
        }

        var currentWriteTimeUtcTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
        _lastValidatedFileStates.TryGetValue(fullPath, out var validated);
        var currentContentHash = ComputeFileHash(fullPath);
        if (validated == null)
        {
            // 无基线（未经加载的存量文件写入）：以当前内容为基线，保持既有可写行为。
            _lastValidatedFileStates[fullPath] = new ValidatedFileState(currentWriteTimeUtcTicks, currentContentHash);
            return;
        }
        if (!string.Equals(validated.ContentHash, currentContentHash, StringComparison.Ordinal))
        {
            throw new StudentWorkbookOverwriteRefusedException(
                $"学生工作簿在加载后被外部修改；拒绝覆盖以避免丢失外部改动，请先重新加载名册或合并外部修改：{fullPath}");
        }

        _lastValidatedFileStates[fullPath] = validated with { WriteTimeUtcTicks = currentWriteTimeUtcTicks };
    }

    private void RecordFileState(string fullPath)
    {
        try
        {
            var writeTimeUtcTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            var contentHash = ComputeFileHash(fullPath);
            _lastValidatedFileStates[fullPath] = new ValidatedFileState(writeTimeUtcTicks, contentHash);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            InfraDiagnosticsLog.Write(
                $"[StudentWorkbookStore] record file state failed path={fullPath} ex={ex.GetType().Name} msg={ex.Message}");
            _lastValidatedFileStates.TryRemove(fullPath, out _);
        }
    }

    private const string BackupFolderName = "backups";
    private const int MaxNormalizationBackups = 10;

    private static bool TryEnsureNormalizationBackup(string path)
    {
        try
        {
            EnsureNormalizationBackup(path);
            return true;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            InfraDiagnosticsLog.Write(
                $"[StudentWorkbookStore] normalization backup failed path={path} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }

    private static void EnsureNormalizationBackup(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        var extension = Path.GetExtension(fullPath);
        var fileName = Path.GetFileNameWithoutExtension(fullPath);
        var contentHash = ComputeFileHash(fullPath);

        // 备份集中写入 backups/ 子目录（兼容旧版散落在数据文件旁的 *.bak-normalize-*.xlsx），
        // 并滚动保留最近 N 份；否则每次规范化前内容都已变化，按哈希去重会失效、备份无限增长。
        var backupDirectory = Path.Combine(directory, BackupFolderName);
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, $"{fileName}.bak-normalize-{contentHash}{extension}");
        if (!File.Exists(backupPath))
        {
            File.Copy(fullPath, backupPath, overwrite: false);
            PruneNormalizationBackups(backupDirectory, fileName, extension);
        }

        var backupHash = ComputeFileHash(backupPath);
        if (!string.Equals(contentHash, backupHash, StringComparison.Ordinal))
        {
            throw new IOException($"学生工作簿迁移备份校验失败：{backupPath}");
        }
    }

    private static void PruneNormalizationBackups(string backupDirectory, string fileNameWithoutExtension, string extension)
    {
        try
        {
            var pattern = $"{fileNameWithoutExtension}.bak-normalize-*{extension}";
            var outdated = Directory.EnumerateFiles(backupDirectory, pattern)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(MaxNormalizationBackups)
                .ToList();
            foreach (var outdatedPath in outdated)
            {
                File.Delete(outdatedPath);
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            InfraDiagnosticsLog.Write(
                $"[StudentWorkbookStore] backup prune failed dir={backupDirectory} ex={ex.GetType().Name} msg={ex.Message}");
        }
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

}
