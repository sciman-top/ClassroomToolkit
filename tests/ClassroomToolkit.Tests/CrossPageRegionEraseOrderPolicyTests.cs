using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageRegionEraseOrderPolicyTests
{
    [Fact]
    public void ResolveBatchOrder_ShouldMoveCurrentPageToTail()
    {
        var result = CrossPageRegionEraseOrderPolicy.ResolveBatchOrder(
            [2, 3, 1],
            currentPage: 2);

        result.Should().Equal(1, 3, 2);
    }

    [Fact]
    public void ResolveBatchOrder_ShouldDistinctAndIgnoreInvalidPages()
    {
        var result = CrossPageRegionEraseOrderPolicy.ResolveBatchOrder(
            [0, -1, 3, 3, 2],
            currentPage: 0);

        result.Should().Equal(2, 3);
    }

    [Fact]
    public void ResolveBatchOrder_ShouldFallbackToCurrent_WhenPagesNull()
    {
        var result = CrossPageRegionEraseOrderPolicy.ResolveBatchOrder(
            null!,
            currentPage: 5);

        result.Should().Equal(5);
    }
}
