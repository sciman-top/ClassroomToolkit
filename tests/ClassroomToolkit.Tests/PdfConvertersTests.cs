using System.Globalization;
using ClassroomToolkit.App.Converters;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PdfConvertersTests
{
    [Fact]
    public void PdfForegroundConverter_ConvertBack_ShouldReturnDoNothing()
    {
        var converter = new PdfForegroundConverter();

        var result = converter.ConvertBack(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture);

        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }

    [Fact]
    public void PdfFontWeightConverter_ConvertBack_ShouldReturnDoNothing()
    {
        var converter = new PdfFontWeightConverter();

        var result = converter.ConvertBack(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture);

        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }
}
