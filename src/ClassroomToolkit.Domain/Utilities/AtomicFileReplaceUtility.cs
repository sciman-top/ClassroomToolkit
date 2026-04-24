namespace ClassroomToolkit.Domain.Utilities;

public static class AtomicFileReplaceUtility
{
    public static void ReplaceOrOverwrite(string tempPath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        try
        {
            File.Replace(tempPath, targetPath, null);
        }
        catch (Exception ex) when (AtomicReplaceFallbackPolicy.ShouldFallback(ex))
        {
            File.Copy(tempPath, targetPath, overwrite: true);
            File.Delete(tempPath);
        }
    }

    public static void WriteAtomically(
        string targetPath,
        Action<string> writeTempFile,
        Action<string, Exception>? onTempCleanupFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(writeTempFile);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
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
