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
}
