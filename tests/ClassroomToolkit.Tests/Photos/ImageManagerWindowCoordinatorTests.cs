using System.Reflection;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Photos;

[Collection("WPF UI")]
public sealed class ImageManagerWindowCoordinatorTests
{
    [Fact]
    public void EnsureWindow_ShouldReuseWindow_AndConnectFeatureEvents()
    {
        WpfStaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var factory = new RecordingFactory();
            var saveCount = 0;
            IReadOnlyList<string>? selectedFavorites = null;
            var coordinator = CreateCoordinator(
                factory,
                settings,
                () =>
                {
                    saveCount++;
                    return true;
                },
                imageSelected: (paths, _) => selectedFavorites = paths);

            var first = coordinator.EnsureWindow();
            var second = coordinator.EnsureWindow();
            var favoritesHandler = GetEventHandler(first, "FavoritesChanged");

            first.Width = 840;
            first.Height = 560;
            favoritesHandler!.DynamicInvoke((object)new[] { "D:\\课堂照片" });

            second.Should().BeSameAs(first);
            factory.CreateCount.Should().Be(1);
            favoritesHandler.Should().NotBeNull("the feature coordinator owns the window subscription");
            settings.PhotoFavoriteFolders.Should().Equal("D:\\课堂照片");
            saveCount.Should().Be(1);
            selectedFavorites.Should().BeNull();

            coordinator.Close();
            GetEventHandler(first, "FavoritesChanged").Should().BeNull("closing must release feature handlers");
            settings.PhotoManagerWindowWidth.Should().Be(840);
            settings.PhotoManagerWindowHeight.Should().Be(560);
            coordinator.CurrentWindow.Should().BeNull();
            saveCount.Should().Be(3, "the closing layout event is persisted before final close-time capture");
        });
    }

    [Fact]
    public void CompleteClose_ShouldIgnoreStaleWindow_WhenANewerWindowIsOwned()
    {
        WpfStaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var factory = new RecordingFactory();
            var saveCount = 0;
            var coordinator = CreateCoordinator(factory, settings, () =>
            {
                saveCount++;
                return true;
            });

            var window = coordinator.EnsureWindow();
            coordinator.CompleteClose(window);
            var nextWindow = coordinator.EnsureWindow();
            coordinator.CompleteClose(window);

            coordinator.CurrentWindow.Should().BeSameAs(nextWindow);
            factory.CreateCount.Should().Be(2);
            saveCount.Should().Be(1);
            nextWindow.Close();
        });
    }

    private static ImageManagerWindowCoordinator CreateCoordinator(
        RecordingFactory factory,
        AppSettings settings,
        Func<bool> saveSettings,
        Action<IReadOnlyList<string>, int>? imageSelected = null)
    {
        return new ImageManagerWindowCoordinator(
            factory,
            settings,
            saveSettings,
            new ImageManagerWindowCallbacks(
                imageSelected ?? ((_, _) => { }),
                (_, _) => { },
                (_, _) => { },
                () => { },
                () => { },
                _ => { }));
    }

    private static Delegate? GetEventHandler(ImageManagerWindow window, string eventName)
    {
        var backingField = typeof(ImageManagerWindow).GetField(
            eventName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (Delegate?)backingField?.GetValue(window);
    }

    private sealed class RecordingFactory : IImageManagerWindowFactory
    {
        internal int CreateCount { get; private set; }

        public ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
        {
            CreateCount++;
            return new ImageManagerWindow(favorites, recents);
        }
    }
}
