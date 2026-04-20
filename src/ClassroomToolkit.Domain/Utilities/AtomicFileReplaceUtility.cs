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
        }
    }
}
