using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayFixtureCatalogTests
{
    [Fact]
    public void Load_ShouldParseCrossPageSeamScenario()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-seam-basic.json");

        scenario.Name.Should().Be("crosspage-seam-basic");
        scenario.Events.Should().NotBeEmpty();
        scenario.Events.Should().Contain(e => e.Type == InkReplayEventType.PointerDown);
        scenario.Events.Should().Contain(e => e.Type == InkReplayEventType.PointerUp);
        scenario.Events.Should().Contain(e => e.Type == InkReplayEventType.CrossPageSwitch);
    }

    [Fact]
    public void Load_ShouldKeepMonotonicTimestampsAndValidPressureRange()
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-seam-basic.json");

        var timestamps = scenario.Events.Select(e => e.TimestampMs).ToArray();
        timestamps.Should().BeInAscendingOrder();

        scenario.Events
            .Where(e => e.Pressure.HasValue)
            .Select(e => e.Pressure!.Value)
            .Should()
            .OnlyContain(p => p >= 0.0 && p <= 1.0);
    }
}

