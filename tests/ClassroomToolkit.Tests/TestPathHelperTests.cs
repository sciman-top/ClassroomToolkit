using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class TestPathHelperTests
{
    [Fact]
    public void ResolveRepoPath_ShouldReturnRepositoryRoot_WhenNoSegments()
    {
        var root = TestPathHelper.GetRepositoryRootOrThrow();

        var resolved = TestPathHelper.ResolveRepoPath();

        resolved.Should().Be(root);
    }

    [Fact]
    public void ResolveRepoPath_ShouldAppendSegments()
    {
        var root = TestPathHelper.GetRepositoryRootOrThrow();

        var resolved = TestPathHelper.ResolveRepoPath(
            "tests",
            "ClassroomToolkit.Tests",
            "TestPathHelper.cs");

        resolved.Should().Be(Path.Combine(root, "tests", "ClassroomToolkit.Tests", "TestPathHelper.cs"));
    }

    [Fact]
    public void ResolveAppPath_ShouldReturnAppRoot_WhenNoSegments()
    {
        var root = TestPathHelper.GetRepositoryRootOrThrow();

        var resolved = TestPathHelper.ResolveAppPath();

        resolved.Should().Be(Path.Combine(root, "src", "ClassroomToolkit.App"));
    }

    [Fact]
    public void ResolveAppPath_ShouldAppendSegments()
    {
        var root = TestPathHelper.GetRepositoryRootOrThrow();

        var resolved = TestPathHelper.ResolveAppPath("Paint", "PaintOverlayWindow.xaml");

        resolved.Should().Be(Path.Combine(root, "src", "ClassroomToolkit.App", "Paint", "PaintOverlayWindow.xaml"));
    }

    [Fact]
    public void GetRelativeRepoPath_ShouldReturnPathRelativeToRepositoryRoot()
    {
        var relative = TestPathHelper.GetRelativeRepoPath(
            TestPathHelper.ResolveRepoPath("tests", "ClassroomToolkit.Tests", "TestPathHelper.cs"));

        relative.Should().Be(Path.Combine("tests", "ClassroomToolkit.Tests", "TestPathHelper.cs"));
    }

    [Fact]
    public void TryCleanupRepositoryTempRoot_ShouldDeleteDirectoriesOlderThanRetention()
    {
        var root = TestPathHelper.CreateIsolatedDirectory("ctool_temp_cleanup_stale_root");
        var staleDirectory = Directory.CreateDirectory(Path.Combine(root, "stale"));
        var freshDirectory = Directory.CreateDirectory(Path.Combine(root, "fresh"));
        staleDirectory.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-10);
        freshDirectory.LastWriteTimeUtc = DateTime.UtcNow;

        TestPathHelper.TryCleanupRepositoryTempRoot(root, DateTime.UtcNow, TimeSpan.FromDays(7), maxEntries: 10);

        Directory.Exists(staleDirectory.FullName).Should().BeFalse();
        Directory.Exists(freshDirectory.FullName).Should().BeTrue();
    }

    [Fact]
    public void TryCleanupRepositoryTempRoot_ShouldTrimOldestDirectoriesWhenOverSoftLimit()
    {
        var root = TestPathHelper.CreateIsolatedDirectory("ctool_temp_cleanup_limit_root");
        var oldest = Directory.CreateDirectory(Path.Combine(root, "oldest"));
        var middle = Directory.CreateDirectory(Path.Combine(root, "middle"));
        var newest = Directory.CreateDirectory(Path.Combine(root, "newest"));
        oldest.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-30);
        middle.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-20);
        newest.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-10);

        TestPathHelper.TryCleanupRepositoryTempRoot(root, DateTime.UtcNow, TimeSpan.FromDays(30), maxEntries: 2);

        Directory.Exists(oldest.FullName).Should().BeFalse();
        Directory.Exists(middle.FullName).Should().BeTrue();
        Directory.Exists(newest.FullName).Should().BeTrue();
    }

    [Fact]
    public void TryCleanupRepositoryTempRoot_ShouldDeleteStaleFilesAndTrimMixedEntries()
    {
        var root = TestPathHelper.CreateIsolatedDirectory("ctool_temp_cleanup_files_root");
        var staleFile = new FileInfo(Path.Combine(root, "stale.ini"));
        File.WriteAllText(staleFile.FullName, "stale");
        staleFile.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-10);
        var olderFile = new FileInfo(Path.Combine(root, "older.ini"));
        File.WriteAllText(olderFile.FullName, "older");
        olderFile.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-20);
        var newestDirectory = Directory.CreateDirectory(Path.Combine(root, "newest"));
        var readOnlyNestedFile = new FileInfo(Path.Combine(newestDirectory.FullName, "readonly.tmp"));
        File.WriteAllText(readOnlyNestedFile.FullName, "readonly");
        readOnlyNestedFile.IsReadOnly = true;
        newestDirectory.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-10);

        var staleReadOnlyDirectory = Directory.CreateDirectory(Path.Combine(root, "stale-readonly"));
        var staleNestedFile = new FileInfo(Path.Combine(staleReadOnlyDirectory.FullName, "readonly.tmp"));
        File.WriteAllText(staleNestedFile.FullName, "readonly");
        staleNestedFile.IsReadOnly = true;
        staleReadOnlyDirectory.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-10);

        TestPathHelper.TryCleanupRepositoryTempRoot(root, DateTime.UtcNow, TimeSpan.FromDays(7), maxEntries: 1);

        File.Exists(staleFile.FullName).Should().BeFalse();
        Directory.Exists(staleReadOnlyDirectory.FullName).Should().BeFalse();
        File.Exists(olderFile.FullName).Should().BeFalse();
        Directory.Exists(newestDirectory.FullName).Should().BeTrue();
    }
}
