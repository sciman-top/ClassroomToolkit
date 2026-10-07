using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Infra.Storage;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StudentResourceLocatorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveDataRoot_ShouldPreserveRealRosterAndRetryAfterSourceUnlock(bool development)
    {
        var root = CreateTempDirectory();
        var legacyPath = Path.Combine(root, "students.xlsx");
        var targetRoot = Path.Combine(root, development ? "data" : "persistent-data");
        var targetPath = Path.Combine(targetRoot, "students.xlsx");
        string ResolveRoot() => development
            ? StudentResourceLocator.ResolveDevelopmentDataRoot(root)
            : StudentResourceLocator.ResolveMigratedDataRoot(root, targetRoot);
        try
        {
            var roster = new ClassRoster(
                "审查班",
                [StudentRecord.Create("9001", "测试学生", "审查班", "A")]);
            var workbook = new StudentWorkbook(
                new Dictionary<string, ClassRoster> { ["审查班"] = roster },
                "审查班");
            var store = new StudentWorkbookStore();
            store.Save(workbook, legacyPath, null);
            var original = File.ReadAllBytes(legacyPath);

            using (var lockedFile = new FileStream(legacyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var resolvedRoot = ResolveRoot();
                resolvedRoot.Should().Be(root);
                var loadWhileLocked = () => store.LoadOrCreate(Path.Combine(resolvedRoot, "students.xlsx"));
                loadWhileLocked.Should().Throw<Exception>();
                File.Exists(targetPath).Should().BeFalse();
            }

            var recoveredRoot = ResolveRoot();
            recoveredRoot.Should().Be(targetRoot);
            var recovered = store.LoadOrCreate(Path.Combine(recoveredRoot, "students.xlsx"));
            recovered.CreatedTemplate.Should().BeFalse();
            recovered.Workbook.ActiveClass.Should().Be("审查班");
            recovered.Workbook.GetActiveRoster().Students.Should().ContainSingle()
                .Which.StudentId.Should().Be("9001");
            File.ReadAllBytes(legacyPath).Should().Equal(original);
            File.ReadAllBytes(targetPath).Should().Equal(original);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDevelopmentDataRoot_ShouldUseLegacyWorkbookWhenPendingCopyCannotBeWritten()
    {
        var root = CreateTempDirectory();
        var targetRoot = Path.Combine(root, "data");
        var pendingPath = Path.Combine(targetRoot, "students.xlsx.migration-pending");
        File.WriteAllText(Path.Combine(root, "students.xlsx"), "real-roster");
        Directory.CreateDirectory(pendingPath);
        try
        {
            StudentResourceLocator.ResolveDevelopmentDataRoot(root).Should().Be(root);
            File.Exists(Path.Combine(targetRoot, "students.xlsx")).Should().BeFalse();
            File.ReadAllText(Path.Combine(root, "students.xlsx")).Should().Be("real-roster");

            Directory.Delete(pendingPath);
            StudentResourceLocator.ResolveDevelopmentDataRoot(root).Should().Be(targetRoot);
            File.ReadAllText(Path.Combine(targetRoot, "students.xlsx")).Should().Be("real-roster");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDevelopmentDataRoot_ShouldKeepCurrentWorkbookWhenPhotoCopyFails()
    {
        var root = CreateTempDirectory();
        var targetRoot = Path.Combine(root, "data");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(root, "students.xlsx"), "legacy");
        File.WriteAllText(Path.Combine(targetRoot, "students.xlsx"), "current");
        Directory.CreateDirectory(Path.Combine(root, "student_photos"));
        File.WriteAllText(Path.Combine(targetRoot, "student_photos"), "blocked-photo-folder");
        try
        {
            StudentResourceLocator.ResolveDevelopmentDataRoot(root).Should().Be(targetRoot);
            File.ReadAllText(Path.Combine(targetRoot, "students.xlsx")).Should().Be("current");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindSolutionDirectory_ShouldReturnAncestorContainingSolutionFile()
    {
        var root = CreateTempDirectory();
        var nested = Path.Combine(root, "src", "ClassroomToolkit.App", "bin", "Debug");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(root, "ClassroomToolkit.sln"), "mock-sln");

        try
        {
            var result = StudentResourceLocator.FindSolutionDirectory(nested);

            result.Should().Be(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindSolutionDirectory_ShouldReturnNull_WhenNoSolutionFileExists()
    {
        var nested = Path.Combine(@"Z:\", $"ctool_locator_no_sln_{Guid.NewGuid():N}", "a", "b", "c");
        var result = StudentResourceLocator.FindSolutionDirectory(nested);

        result.Should().BeNull();
    }

    [Fact]
    public void FindSolutionDirectory_ShouldSkipInvalidStartPaths()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ClassroomToolkit.sln"), "mock-sln");

        try
        {
            var result = StudentResourceLocator.FindSolutionDirectory("bad\0path", root);

            result.Should().Be(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveStudentPhotoRoot_ShouldEnsureDefaultClassFolderExists()
    {
        var root = CreateTempDirectory();

        try
        {
            var photoRoot = StudentResourceLocator.PrepareStudentPhotoRoot(root);
            var defaultClassFolder = Path.Combine(photoRoot, "1班");

            Directory.Exists(photoRoot).Should().BeTrue();
            Directory.Exists(defaultClassFolder).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDevelopmentDataRoot_ShouldPreferDataFolderOverLegacyRoot()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ClassroomToolkit.sln"), "mock-sln");
        File.WriteAllText(Path.Combine(root, "students.xlsx"), "legacy");
        Directory.CreateDirectory(Path.Combine(root, "student_photos"));
        Directory.CreateDirectory(Path.Combine(root, "data", "student_photos"));
        File.WriteAllText(Path.Combine(root, "data", "students.xlsx"), "current");

        try
        {
            var result = StudentResourceLocator.ResolveDevelopmentDataRoot(root);

            Path.GetFileName(result).Should().Be("data");
            File.ReadAllText(Path.Combine(result, "students.xlsx")).Should().Be("current");
            File.ReadAllText(Path.Combine(root, "students.xlsx")).Should().Be("legacy");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDevelopmentDataRoot_ShouldCopyLegacyRootDataIntoDataFolderOnce()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ClassroomToolkit.sln"), "mock-sln");
        File.WriteAllText(Path.Combine(root, "students.xlsx"), "legacy-workbook");
        Directory.CreateDirectory(Path.Combine(root, "student_photos", "1班"));
        File.WriteAllText(Path.Combine(root, "student_photos", "1班", "001.jpg"), "photo-bytes");

        try
        {
            var result = StudentResourceLocator.ResolveDevelopmentDataRoot(root);

            File.ReadAllText(Path.Combine(result, "students.xlsx")).Should().Be("legacy-workbook");
            File.ReadAllText(Path.Combine(result, "student_photos", "1班", "001.jpg")).Should().Be("photo-bytes");
            File.Exists(Path.Combine(root, "students.xlsx")).Should().BeTrue();

            // A second resolution must not overwrite classroom data that has since changed.
            File.WriteAllText(Path.Combine(result, "students.xlsx"), "edited-in-data");
            StudentResourceLocator.ResolveDevelopmentDataRoot(root);
            File.ReadAllText(Path.Combine(result, "students.xlsx")).Should().Be("edited-in-data");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDevelopmentDataRoot_ShouldMergeMissingLegacyPhotosWithoutOverwritingDataFolder()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ClassroomToolkit.sln"), "mock-sln");
        Directory.CreateDirectory(Path.Combine(root, "student_photos", "1班"));
        File.WriteAllText(Path.Combine(root, "student_photos", "1班", "001.jpg"), "legacy-photo");
        File.WriteAllText(Path.Combine(root, "student_photos", "1班", "002.jpg"), "missing-photo");
        Directory.CreateDirectory(Path.Combine(root, "data", "student_photos", "1班"));
        File.WriteAllText(Path.Combine(root, "data", "student_photos", "1班", "001.jpg"), "current-photo");

        try
        {
            var result = StudentResourceLocator.ResolveDevelopmentDataRoot(root);

            File.ReadAllText(Path.Combine(result, "student_photos", "1班", "001.jpg")).Should().Be("current-photo");
            File.ReadAllText(Path.Combine(result, "student_photos", "1班", "002.jpg")).Should().Be("missing-photo");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        return TestPathHelper.CreateDirectory("ctool_locator");
    }
}
