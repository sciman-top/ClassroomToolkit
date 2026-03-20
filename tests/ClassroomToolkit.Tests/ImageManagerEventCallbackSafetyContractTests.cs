using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerEventCallbackSafetyContractTests
{
    [Fact]
    public void ImageManagerCallbacks_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites))");
        source.Should().Contain("RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents))");
        source.Should().Contain("ImageSelected?.Invoke(GetNavigablePaths(), ViewModel.CurrentIndex)");
        source.Should().Contain("ImageManager: favorites callback failed");
        source.Should().Contain("ImageManager: recents callback failed");
        source.Should().Contain("ImageManager: image selected callback failed");
        source.Should().Contain("ImageManager: layout callback failed");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.xaml.cs");
    }
}
