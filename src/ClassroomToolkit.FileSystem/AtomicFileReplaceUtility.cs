namespace ClassroomToolkit.IO;

/// <summary>
/// Writes complete temporary files and atomically replaces their targets.
/// This shared file-system mechanism is used by both Infra adapters and App ink storage.
/// </summary>
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

    // Flush before replacement so a completed rename does not expose file data that was
    // still only in the OS cache when the process or machine stopped unexpectedly.
    private static void FlushTempFileToDisk(string tempPath)
    {
        try
        {
            using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            // A failed flush must not block the write; this preserves the prior best-effort behavior.
        }
    }

    public static void WriteAtomically(
        string targetPath,
        Action<string> writeTempFile,
        Action<string, Exception>? onTempCleanupFailure = null)
    {
        WriteAtomically(targetPath, ".tmp", writeTempFile, onTempCleanupFailure);
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
                catch (Exception ex) when (IsNonFatal(ex))
                {
                    onTempCleanupFailure?.Invoke(tempPath, ex);
                }
            }
        }
    }

    private static bool IsNonFatal(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex is not (
            OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException);
    }
}
