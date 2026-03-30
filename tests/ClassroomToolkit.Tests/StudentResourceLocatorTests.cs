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

    private static string CreateTempDirectory()
    {
        return TestPathHelper.CreateDirectory("ctool_locator");
    }
}
