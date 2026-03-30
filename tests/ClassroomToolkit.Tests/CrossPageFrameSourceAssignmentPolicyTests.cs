using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageFrameSourceAssignmentPolicyTests
{
    [Fact]
    public void ShouldAssign_ShouldReturnFalse_WhenSameReferenceAndNotForced()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);

        var result = CrossPageFrameSourceAssignmentPolicy.ShouldAssign(bitmap, bitmap);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAssign_ShouldReturnTrue_WhenClearingExistingSource()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);

        var result = CrossPageFrameSourceAssignmentPolicy.ShouldAssign(bitmap, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldAssign_ShouldReturnFalse_WhenBothSourcesNull()
    {
        var result = CrossPageFrameSourceAssignmentPolicy.ShouldAssign(null, null);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAssign_ShouldReturnTrue_WhenForced()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);

        var result = CrossPageFrameSourceAssignmentPolicy.ShouldAssign(bitmap, bitmap, forceAssign: true);

        result.Should().BeTrue();
    }
}
