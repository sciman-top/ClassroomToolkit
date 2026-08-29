namespace ClassroomToolkit.Domain.Utilities;

public static class AtomicFileReplaceUtility
{
    private const int TransientReplaceRetryCount = 3;
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
            if (File.Exists(targetPath))
            {
                ReplaceOrOverwrite(tempPath, targetPath);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
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
