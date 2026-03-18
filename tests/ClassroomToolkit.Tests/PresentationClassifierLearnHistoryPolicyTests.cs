using ClassroomToolkit.App.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationClassifierLearnHistoryPolicyTests
{
    [Fact]
    public void Append_ShouldKeepRecentFiveRecords()
    {
        var json = string.Empty;
        for (var i = 0; i < 7; i++)
        {
            json = PresentationClassifierLearnHistoryPolicy.Append(
                json,
                new DateTime(2026, 3, 18, 8, 0, i, DateTimeKind.Utc),
                $"detail-{i}");
        }

        var records = PresentationClassifierLearnHistoryPolicy.Parse(json);

        records.Count.Should().Be(5);
        records[0].Detail.Should().Be("detail-2");
        records[^1].Detail.Should().Be("detail-6");
    }

    [Fact]
    public void Parse_ShouldReturnEmpty_WhenJsonInvalid()
    {
        var records = PresentationClassifierLearnHistoryPolicy.Parse("{not-json");

        records.Should().BeEmpty();
    }
}
