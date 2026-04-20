using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BoardPrimaryActionTests
{
    [Theory]
    [InlineData("CaptureRegion", "CaptureRegion")]
    [InlineData("EnterWhiteboard", "EnterWhiteboard")]
    public void BoardPrimaryAction_ShouldRoundTrip(string value, string expected)
    {
        Enum.Parse<BoardPrimaryAction>(value).Should().Be(Enum.Parse<BoardPrimaryAction>(expected));
    }
}
