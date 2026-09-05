using ClassroomToolkit.Domain.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AtomicFileReplaceUtilityTests
{
    [Fact]
    public void ReplaceOrOverwrite_ShouldReplaceTargetContent()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_atomic_replace");
        var targetPath = Path.Combine(rootPath, "settings.json");
        var tempPath = Path.Combine(rootPath, "settings.json.tmp");
        File.WriteAllText(targetPath, "old");
        File.WriteAllText(tempPath, "new");

        try
        {
            AtomicFileReplaceUtility.ReplaceOrOverwrite(tempPath, targetPath);

            File.ReadAllText(targetPath).Should().Be("new");
            File.Exists(tempPath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void WriteAtomically_ShouldReplaceTargetContent_AndCleanupTempFile()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_atomic_write");
        var targetPath = Path.Combine(rootPath, "settings.json");
        File.WriteAllText(targetPath, "old");

        try
        {
            AtomicFileReplaceUtility.WriteAtomically(
                targetPath,
                tempPath => File.WriteAllText(tempPath, "new"));

            File.ReadAllText(targetPath).Should().Be("new");
            Directory.GetFiles(rootPath, $"{Path.GetFileName(targetPath)}.*.tmp").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void WriteAtomically_ShouldCreateTargetWhenItDoesNotExist()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_atomic_write_new");
        var targetPath = Path.Combine(rootPath, "settings.json");

        try
        {
            AtomicFileReplaceUtility.WriteAtomically(
                targetPath,
                tempPath => File.WriteAllText(tempPath, "new"));

            File.ReadAllText(targetPath).Should().Be("new");
            Directory.GetFiles(rootPath, $"{Path.GetFileName(targetPath)}.*.tmp").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void WriteAtomically_ShouldPreserveRequestedTempFileExtension()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_atomic_write_ext");
        var targetPath = Path.Combine(rootPath, "students");
        string? observedTempPath = null;

        try
        {
            AtomicFileReplaceUtility.WriteAtomically(
                targetPath,
                ".xlsx",
                tempPath =>
                {
                    observedTempPath = tempPath;
                    File.WriteAllText(tempPath, "new");
                });

            observedTempPath.Should().NotBeNull();
            observedTempPath!.Should().EndWith(".tmp.xlsx");
            File.ReadAllText(targetPath).Should().Be("new");
            Directory.GetFiles(rootPath, $"{Path.GetFileName(targetPath)}.*.tmp.xlsx").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void WriteAtomically_ShouldCleanupTempFile_WhenReplaceFails()
    {
        var targetPath = TestPathHelper.CreateFilePath("ctool_atomic_write_cleanup", ".json");
        var rootPath = Path.GetDirectoryName(targetPath)!;
        File.WriteAllText(targetPath, "old");

        try
        {
            using var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Action act = () => AtomicFileReplaceUtility.WriteAtomically(
                targetPath,
                tempPath => File.WriteAllText(tempPath, "new"));

            act.Should().Throw<Exception>().Where(ex =>
                ex.GetType() == typeof(IOException)
                || ex.GetType() == typeof(UnauthorizedAccessException));
            Directory.GetFiles(rootPath, $"{Path.GetFileName(targetPath)}.*.tmp").Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public async Task WriteAtomically_ShouldRetryWhenTargetLockIsReleasedQuickly()
    {
        var targetPath = TestPathHelper.CreateFilePath("ctool_atomic_write_retry", ".json");
        var rootPath = Path.GetDirectoryName(targetPath)!;
        File.WriteAllText(targetPath, "old");
        var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var releaseStarted = new ManualResetEventSlim();
        var releaseComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseThread = new Thread(() =>
        {
            try
            {
                releaseStarted.Set();
                Thread.Sleep(25);
                lockStream.Dispose();
                releaseComplete.TrySetResult(true);
            }
            catch (Exception ex)
            {
                releaseComplete.TrySetException(ex);
            }
        })
        {
            IsBackground = true
        };
        releaseThread.Start();
        releaseStarted.Wait(TestContext.Current.CancellationToken);

        try
        {
            AtomicFileReplaceUtility.WriteAtomically(
                targetPath,
                tempPath => File.WriteAllText(tempPath, "new"));

            await releaseComplete.Task;
            File.ReadAllText(targetPath).Should().Be("new");
            Directory.GetFiles(rootPath, $"{Path.GetFileName(targetPath)}.*.tmp").Should().BeEmpty();
        }
        finally
        {
            lockStream.Dispose();
            await releaseComplete.Task;
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }
}
