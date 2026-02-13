using System.Globalization;
using System.Windows.Data;
using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerConvertersTests
{
    [Fact]
    public void FolderVisibilityConverter_ConvertBack_ShouldReturnDoNothing()
    {
        var converter = new FolderVisibilityConverter();

        var result = converter.ConvertBack(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture);

        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }

    [Fact]
    public void FileVisibilityConverter_ConvertBack_ShouldReturnDoNothing()
    {
        var converter = new FileVisibilityConverter();

        var result = converter.ConvertBack(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture);

        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }

    [Fact]
    public void PdfBackgroundConverter_ConvertBack_ShouldReturnDoNothing()
    {
        var converter = new PdfBackgroundConverter();

        var result = converter.ConvertBack(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture);

        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }
}
