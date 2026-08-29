using ClassroomToolkit.App.Helpers;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StudentResourceLocatorTests
{
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
