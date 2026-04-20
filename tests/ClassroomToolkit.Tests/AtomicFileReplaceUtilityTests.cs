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
}
