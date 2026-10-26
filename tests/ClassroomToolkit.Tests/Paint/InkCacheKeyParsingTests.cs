using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class InkCacheKeyParsingTests
{
    [Theory]
    [InlineData("img|C:\\data\\a.png", true, "C:\\data\\a.png", 1)]
    [InlineData("pdf|C:\\data\\deck.pdf|page_3", true, "C:\\data\\deck.pdf", 3)]
    [InlineData("pdf|C:\\data\\deck.pdf|page_003", true, "C:\\data\\deck.pdf", 3)]
    [InlineData("pdf|C:\\data\\deck.pdf|page_x", false, "", 1)]
    [InlineData("unknown|abc", false, "", 1)]
    public void TryParseCacheKey_ShouldParseExpectedValues(string key, bool expectedOk, string expectedPath, int expectedPage)
    {
        var ok = InkExportSnapshotBuilder.TryParseCacheKey(key, out var path, out var page);

        ok.Should().Be(expectedOk);
        if (expectedOk)
        {
            path.Should().Be(expectedPath);
            page.Should().Be(expectedPage);
        }
    }
}
