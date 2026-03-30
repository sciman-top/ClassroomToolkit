using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchTargetPolicyTests
{
    [Theory]
    [InlineData(3, 3, 3)]
    [InlineData(3, 8, 4)]
    [InlineData(3, 1, 2)]
    [InlineData(1, -5, 0)]
    public void ResolveNeighborTargetPage_ShouldMatchExpected(
        int currentPage,
        int requestedPage,
        int expected)
    {
        var target = CrossPageInputSwitchTargetPolicy.ResolveNeighborTargetPage(
            currentPage,
            requestedPage);

        target.Should().Be(expected);
    }
}
