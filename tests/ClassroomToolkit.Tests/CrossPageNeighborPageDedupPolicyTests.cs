using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPageDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSameList_WhenNoDuplicates()
    {
        var input = new List<(int PageIndex, double Top)>
        {
            (2, -100),
            (3, 200)
        };

        var result = CrossPageNeighborPageDedupPolicy.Resolve(input);

        result.Should().Equal(input);
    }

    [Fact]
    public void Resolve_ShouldKeepFirstOccurrence_WhenDuplicatesExist()
    {
        var result = CrossPageNeighborPageDedupPolicy.Resolve(
            new List<(int PageIndex, double Top)>
            {
                (2, -120),
                (3, 180),
                (2, -110),
                (4, 520),
                (3, 200)
            });

        result.Should().Equal(
            new List<(int PageIndex, double Top)>
            {
                (2, -120),
                (3, 180),
                (4, 520)
            });
    }
}

