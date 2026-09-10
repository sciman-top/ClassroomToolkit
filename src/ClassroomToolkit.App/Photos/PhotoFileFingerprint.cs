using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace ClassroomToolkit.App.Photos;

internal readonly record struct PhotoFileFingerprint(
    long Length,
    long LastWriteTimeUtcTicks,
    string ContentHash)
{
    public bool Matches(PhotoFileFingerprint other)
    {
        return Length == other.Length
            && LastWriteTimeUtcTicks == other.LastWriteTimeUtcTicks
            && string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal);
    }
}

internal static class PhotoFileFingerprintReader
{
    private const int BufferSize = 64 * 1024;
    private const int StableReadAttempts = 2;

    public static bool TryRead(string path, out PhotoFileFingerprint fingerprint)
    {
        fingerprint = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        for (var attempt = 0; attempt < StableReadAttempts; attempt++)
        {
            try
            {
                var beforeWriteTimeUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    BufferSize,
                    FileOptions.SequentialScan);
                var length = stream.Length;
                var contentHash = Convert.ToHexString(SHA256.HashData(stream));
                var afterLength = new FileInfo(path).Length;
                var afterWriteTimeUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;

                // If a teacher is replacing the file while it is being read, do not
                // associate the hash with a partially observed cache entry.
                if (length != afterLength || beforeWriteTimeUtcTicks != afterWriteTimeUtcTicks)
                {
                    continue;
                }

                fingerprint = new PhotoFileFingerprint(length, afterWriteTimeUtcTicks, contentHash);
                return true;
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine(
                    $"PhotoFileFingerprintReader: failed to read '{path}', attempt={attempt + 1}, reason={ex.GetType().Name}:{ex.Message}");
                return false;
            }
        }

        Debug.WriteLine($"PhotoFileFingerprintReader: file changed while reading '{path}'.");
        return false;
    }
}
