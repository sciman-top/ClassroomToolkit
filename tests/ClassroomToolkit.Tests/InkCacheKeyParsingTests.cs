using System.Reflection;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

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
        var type = Type.GetType("ClassroomToolkit.App.Paint.PaintOverlayWindow, ClassroomToolkit.App");
        type.Should().NotBeNull();

        var method = type!.GetMethod(
            "TryParseCacheKey",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var args = new object[] { key, "", 1 };
        var ok = (bool)method!.Invoke(null, args)!;

        ok.Should().Be(expectedOk);
        if (expectedOk)
        {
            args[1].Should().Be(expectedPath);
            args[2].Should().Be(expectedPage);
        }
    }
}
