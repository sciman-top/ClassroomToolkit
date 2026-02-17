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
        var root = CreateTempDirectory();
        var nested = Path.Combine(root, "a", "b", "c");
        Directory.CreateDirectory(nested);

        try
        {
            var result = StudentResourceLocator.FindSolutionDirectory(nested);

            result.Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_locator_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
