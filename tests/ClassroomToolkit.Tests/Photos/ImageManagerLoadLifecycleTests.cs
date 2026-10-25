using System.Reflection;
using System.Windows.Media;
using ClassroomToolkit.App.Photos;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Photos;

[Collection("WPF UI")]
public sealed class ImageManagerLoadLifecycleTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    [Fact]
    public void LoadThumbnail_ShouldDecodeFromCombinedContentRead()
    {
        WpfStaTestRunner.Run(() =>
        {
            var method = typeof(ImageManagerWindow).GetMethod(
                "LoadThumbnail",
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                new[] { typeof(byte[]), typeof(int) },
                modifiers: null);
            method.Should().NotBeNull();

            var thumbnail = method!.Invoke(
                null,
                [Convert.FromBase64String(OnePixelPngBase64), 120]) as ImageSource;
            thumbnail.Should().NotBeNull();
        });
    }

    [Fact]
    public void ScanDirectory_ShouldKeepLegacyOrderingAndFiltering()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctk_image_manager_scan");
        var folderPath = Path.Combine(rootPath, "folder");
        var pdfPath = Path.Combine(rootPath, "b.pdf");
        var imagePath = Path.Combine(rootPath, "a.png");
        var hiddenPath = Path.Combine(rootPath, "hidden.jpg");
        var dotPath = Path.Combine(rootPath, ".hidden.png");
        var ignoredPath = Path.Combine(rootPath, "ignored.txt");

        try
        {
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(pdfPath, "pdf");
            File.WriteAllText(imagePath, "image");
            File.WriteAllText(hiddenPath, "hidden");
            File.WriteAllText(dotPath, "dot");
            File.WriteAllText(ignoredPath, "ignored");
            File.SetAttributes(hiddenPath, FileAttributes.Hidden);

            var method = typeof(ImageManagerWindow).GetMethod(
                "ScanDirectory",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var result = method!.Invoke(null, [rootPath, CancellationToken.None]) as List<ImageItem>;
            result.Should().NotBeNull();
            result!.Select(item => item.Path).Should().Equal(folderPath, pdfPath, imagePath);
        }
        finally
        {
            if (File.Exists(hiddenPath))
            {
                File.SetAttributes(hiddenPath, FileAttributes.Normal);
            }

            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task LoadImages_ShouldStop_WhenCanceledDuringPreviousCancellation()
    {
        using var releaseCallback = new ManualResetEventSlim();
        using var previous = new CancellationTokenSource();
        using var registration = previous.Token.Register(() => releaseCallback.Wait(TimeSpan.FromSeconds(10)));
        ImageManagerWindow? window = null;
        Task? load = null;
        try
        {
            WpfStaTestRunner.Run(() =>
            {
                window = new ImageManagerWindow([], []);
                typeof(ImageManagerWindow).GetField("_thumbnailCts", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(window, previous);
                load = (Task)typeof(ImageManagerWindow).GetMethod("LoadImagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(window, [Path.GetTempPath(), 0])!;
                // The same cleanup runs when the teacher leaves a folder while its
                // previous thumbnail cancellation callbacks are still executing.
                typeof(ImageManagerWindow).GetMethod("ShowEmptyState", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(window, null);
            });
            releaseCallback.Set();

            await load!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            WpfStaTestRunner.Run(() => window!.ViewModel.Images.Should().BeEmpty());
        }
        finally
        {
            releaseCallback.Set();
            if (window != null)
            {
                WpfStaTestRunner.Run(() => window.Close());
            }
        }
    }
}
