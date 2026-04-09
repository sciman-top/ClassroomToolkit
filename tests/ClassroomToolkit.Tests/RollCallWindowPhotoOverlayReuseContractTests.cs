using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallWindowPhotoOverlayReuseContractTests
{
    [Fact]
    public void UpdatePhotoDisplay_ShouldReuseOverlay_AndUseViewModelPhotoPath()
    {
        var source = File.ReadAllText(GetSourcePath());
        var updateStart = source.IndexOf("private void UpdatePhotoDisplay", StringComparison.Ordinal);
        var resolveFromViewModelIndex = source.IndexOf("var path = _viewModel.CurrentStudentPhotoPath;", StringComparison.Ordinal);
        var ensureOverlayIndex = source.IndexOf("var overlay = EnsurePhotoOverlay();", StringComparison.Ordinal);

        updateStart.Should().BeGreaterThan(0);
        resolveFromViewModelIndex.Should().BeGreaterThan(updateStart);
        ensureOverlayIndex.Should().BeGreaterThan(resolveFromViewModelIndex);
        source.Should().NotContain("_lastPhotoStudentId");
        source.Should().NotContain("ClosePhotoOverlay();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Photo.cs");
    }
}
