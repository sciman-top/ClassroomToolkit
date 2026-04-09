using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerEventCallbackSafetyContractTests
{
    [Fact]
    public void ImageManagerCallbacks_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow*.cs");

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites))");
        source.Should().Contain("RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents))");
        source.Should().Contain("ImageSelected?.Invoke(GetNavigablePaths(), ViewModel.CurrentIndex)");
        source.Should().Contain("ImageManager: favorites callback failed");
        source.Should().Contain("ImageManager: recents callback failed");
        source.Should().Contain("ImageManager: image selected callback failed");
        source.Should().Contain("ImageManager: layout callback failed");
    }
}
