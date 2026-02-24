using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OneEuroFilterTests
{
    [Fact]
    public void Filter_ShouldTrackConstantSignal()
    {
        var filter = new OneEuroFilter(1.2, 0.05);
        var value = 0.0;
        for (int i = 0; i < 30; i++)
        {
            value = filter.Filter(10.0, 1.0 / 120.0);
        }

        value.Should().BeApproximately(10.0, 0.001);
    }

    [Fact]
    public void PointFilter_ShouldReduceLargeJump()
    {
        var filter = new OneEuroPointFilter(1.0, 0.08);
        _ = filter.Filter(new System.Windows.Point(0, 0), 1.0 / 120.0);
        var filtered = filter.Filter(new System.Windows.Point(100, 0), 1.0 / 120.0);

        filtered.X.Should().BeGreaterThan(0);
        filtered.X.Should().BeLessThan(100);
    }
}
