using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AwesomeAssertions;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.Tests.Photos;

public sealed class PhotoOverlayLoadBitmapDecodeContractTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    [Fact]
    public void LoadBitmap_ShouldAvoidIgnoreImageCache_AndApplyDecodePixelWidth()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("bitmap.DecodePixelWidth = decodePixelWidth;");
        source.Should().Contain("ResolveDecodePixelWidth()");
        source.Should().NotContain("BitmapCreateOptions.IgnoreImageCache");
    }

    [Fact]
    public void LoadBitmapAsync_ShouldDecodeAndFingerprintFromCombinedContentRead()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctk-photo-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));
        try
        {
            WpfStaTestRunner.Run(() =>
            {
                var method = typeof(PhotoOverlayWindow).GetMethod(
                    "LoadBitmapAsync",
                    BindingFlags.NonPublic | BindingFlags.Static);
                method.Should().NotBeNull();

                var task = (Task)method!.Invoke(null, [path])!;
                task.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

                var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                var bitmap = result.GetType().GetProperty("Bitmap")!.GetValue(result) as BitmapSource;
                var fingerprint = result.GetType().GetProperty("Fingerprint")!.GetValue(result);
                bitmap.Should().NotBeNull();
                bitmap!.PixelWidth.Should().BeGreaterThan(0);
                fingerprint.Should().NotBeNull();
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }
}
