using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallWindowPhotoOverlayReuseContractTests
{
    [Fact]
    public void UpdatePhotoDisplay_ShouldCloseExistingOverlay_WhenStudentChanges()
    {
        var source = File.ReadAllText(GetSourcePath());
        var updateStart = source.IndexOf("private void UpdatePhotoDisplay", StringComparison.Ordinal);
        var ensureResolverIndex = source.IndexOf("var resolver = EnsurePhotoResolver();", StringComparison.Ordinal);
        var closeOverlayIndex = source.IndexOf("ClosePhotoOverlay();", StringComparison.Ordinal);
        var ensureOverlayIndex = source.IndexOf("var overlay = EnsurePhotoOverlay();", StringComparison.Ordinal);

        updateStart.Should().BeGreaterThan(0);
        ensureResolverIndex.Should().BeGreaterThan(updateStart);
        closeOverlayIndex.Should().BeGreaterThan(updateStart);
        ensureOverlayIndex.Should().BeGreaterThan(closeOverlayIndex);
        source.Should().Contain("!string.Equals(_lastPhotoStudentId, studentId, StringComparison.OrdinalIgnoreCase)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Photo.cs");
    }
}
