using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoCrossPageSequencePolicyTests
{
    [Fact]
    public void Normalize_ShouldKeepOnlyImages_AndMapCurrentIndexByPath()
    {
        var sequence = new[]
        {
            @"E:\a.pdf",
            @"E:\b.png",
            @"E:\c.jpg"
        };

        var (normalized, index) = PhotoCrossPageSequencePolicy.Normalize(sequence, currentIndex: 1);

        normalized.Should().Equal(@"E:\b.png", @"E:\c.jpg");
        index.Should().Be(0);
    }

    [Fact]
    public void Normalize_ShouldReturnEmpty_WhenNoImageInSequence()
    {
        var sequence = new[]
        {
            @"E:\a.pdf",
            @"E:\b.pdf"
        };

        var (normalized, index) = PhotoCrossPageSequencePolicy.Normalize(sequence, currentIndex: 0);

        normalized.Should().BeEmpty();
        index.Should().Be(-1);
    }
}
