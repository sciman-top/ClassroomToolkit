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
}
