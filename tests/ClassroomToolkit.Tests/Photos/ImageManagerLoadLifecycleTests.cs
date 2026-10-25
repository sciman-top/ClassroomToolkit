using System.Reflection;
using ClassroomToolkit.App.Photos;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Photos;

[Collection("WPF UI")]
public sealed class ImageManagerLoadLifecycleTests
{
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
