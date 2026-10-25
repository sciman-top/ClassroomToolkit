using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AwesomeAssertions;
using ClassroomToolkit.App.RollCall;

namespace ClassroomToolkit.Tests.RollCall;

/// <summary>
/// 点名窗照片预览（P2-2）的受控验收：转换器是非空字符串语义，
/// 且解码路径必须降采样+冻结，避免预览可见后每次点名全尺寸同步解码。
/// </summary>
public sealed class RollCallPhotoConvertersTests
{
    [Fact]
    public void VisibilityConverter_ShouldCollapseOnNullNonStringAndBlankPath()
    {
        var converter = new PhotoPathToVisibilityConverter();

        converter.Convert(null, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        converter.Convert(string.Empty, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        converter.Convert("   ", typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        // 原始缺陷类别：绑定值不是 string（曾被误接 BooleanToVisibilityConverter）
        converter.Convert(42, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void VisibilityConverter_ShouldShowOnNonEmptyPath_AndIsOneWay()
    {
        var converter = new PhotoPathToVisibilityConverter();

        converter.Convert(@"C:\photos\student.png", typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);

        var convertBack = () => converter.ConvertBack(Visibility.Visible, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        convertBack.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ImageSourceConverter_ShouldDecodeDownsampledFrozenBitmap()
    {
        WpfStaTestRunner.Run(() =>
        {
            var path = CreateTestPng(2000, 3000);
            try
            {
                var converter = new PhotoPathToImageSourceConverter();
                var result = converter.Convert(path, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.InvariantCulture);

                var bitmap = result.Should().BeOfType<BitmapImage>().Subject;
                bitmap.IsFrozen.Should().BeTrue();
                bitmap.PixelWidth.Should().BeLessThanOrEqualTo(512);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void ImageSourceConverter_ShouldReturnNull_WhenFileMissingOrNotAnImage()
    {
        WpfStaTestRunner.Run(() =>
        {
            var converter = new PhotoPathToImageSourceConverter();

            converter.Convert(null, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeNull();
            converter.Convert(string.Empty, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeNull();

            var missing = TestPathHelper.CreateFilePath("ctool_photo_missing", ".png");
            converter.Convert(missing, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeNull();

            var notImage = TestPathHelper.CreateFilePath("ctool_photo_not_image", ".png");
            File.WriteAllText(notImage, "this is not an image");
            try
            {
                converter.Convert(notImage, typeof(System.Windows.Media.ImageSource), null, System.Globalization.CultureInfo.InvariantCulture)
                    .Should().BeNull();
            }
            finally
            {
                File.Delete(notImage);
            }
        });
    }

    [Fact]
    public void ImageSourceConverter_IsOneWay()
    {
        var converter = new PhotoPathToImageSourceConverter();
        var convertBack = () => converter.ConvertBack(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        convertBack.Should().Throw<NotSupportedException>();
    }

    private static string CreateTestPng(int width, int height)
    {
        var path = TestPathHelper.CreateFilePath("ctool_photo_preview", ".png");
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Red, null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
