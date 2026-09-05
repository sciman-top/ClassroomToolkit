namespace ClassroomToolkit.Domain.Utilities;

public static class AtomicFileReplaceUtility
{
    private const int TransientReplaceRetryCount = 5;
    private const int TransientReplaceRetryDelayMilliseconds = 50;

    public static void ReplaceOrOverwrite(string tempPath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                ReplaceOrOverwriteCore(tempPath, targetPath);
                return;
            }
            catch (IOException) when (attempt < TransientReplaceRetryCount)
            {
                Thread.Sleep(TransientReplaceRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException) when (attempt < TransientReplaceRetryCount)
            {
                Thread.Sleep(TransientReplaceRetryDelayMilliseconds);
            }
        }
    }

    private static void ReplaceOrOverwriteCore(string tempPath, string targetPath)
    {
        try
        {
            File.Replace(tempPath, targetPath, null);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch (FileNotFoundException) when (File.Exists(tempPath) && !File.Exists(targetPath))
        {
            // File.Replace requires an existing destination on platforms that support it.
            // If the destination is absent, move the completed temp file without overwrite;
            // a destination that appears concurrently is handled by the outer retry loop.
            File.Move(tempPath, targetPath);
        }
    }

    // 断电/强杀场景：仅 Close 不保证数据离开 OS 缓存，File.Replace 可能替换成功而
    // 目标内容仍是未冲刷的截断数据。替换前对临时文件强制 flush-to-disk。
    private static void FlushTempFileToDisk(string tempPath)
    {
        try
        {
            using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }
        catch (Exception ex) when (DomainExceptionFilterPolicy.IsNonFatal(ex))
        {
            // 冲刷失败不阻断替换；最坏情形等同旧行为（崩溃窗口内内容依赖 OS 缓存）。
        }
    }

    public static void WriteAtomically(
        string targetPath,
        Action<string> writeTempFile,
        Action<string, Exception>? onTempCleanupFailure = null)
    {
        WriteAtomically(
            targetPath,
            ".tmp",
            writeTempFile,
            onTempCleanupFailure);
    }

    public static void WriteAtomically(
        string targetPath,
        string tempFileExtension,
        Action<string> writeTempFile,
        Action<string, Exception>? onTempCleanupFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempFileExtension);
        ArgumentNullException.ThrowIfNull(writeTempFile);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalizedTempExtension = tempFileExtension[0] == '.'
            ? tempFileExtension
            : $".{tempFileExtension}";
        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp{normalizedTempExtension}";
        try
        {
            writeTempFile(tempPath);
            FlushTempFileToDisk(tempPath);
            ReplaceOrOverwrite(tempPath, targetPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (DomainExceptionFilterPolicy.IsNonFatal(ex))
                {
                    onTempCleanupFailure?.Invoke(tempPath, ex);
                }
            }
        }
    }
}
