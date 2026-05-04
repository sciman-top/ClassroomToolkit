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
        source.Should().Contain("private void NotifyFavoritesChanged()");
        source.Should().Contain("private void NotifyRecentsChanged()");
        source.Should().Contain("FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites))");
        source.Should().Contain("RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents))");
        source.Should().Contain("ImageSelected?.Invoke(GetNavigablePaths(), ViewModel.CurrentIndex)");
        source.Should().Contain("ImageManager: favorites callback failed");
        source.Should().Contain("ImageManager: recents callback failed");
        source.Should().Contain("ImageManager: image selected callback failed");
        source.Should().Contain("ImageManager: layout callback failed");
        ContractSourceAggregateLoader
            .CountOccurrences(source, "FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites))")
            .Should()
            .Be(1);
        ContractSourceAggregateLoader
            .CountOccurrences(source, "RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents))")
            .Should()
            .Be(1);
    }
}
