using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageUpdateSourceParserTests
{
    [Fact]
    public void Parse_ShouldNormalizeImmediateSuffix()
    {
        var result = CrossPageUpdateSourceParser.Parse(
            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.NeighborRender));

        result.BaseSource.Should().Be(CrossPageUpdateSources.NeighborRender);
        result.Suffix.Should().Be(CrossPageUpdateDispatchSuffix.Immediate);
    }

    [Fact]
    public void Parse_ShouldNormalizeDelayedSuffix()
    {
        var result = CrossPageUpdateSourceParser.Parse(
            CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.InkStateChanged));

        result.BaseSource.Should().Be(CrossPageUpdateSources.InkStateChanged);
        result.Suffix.Should().Be(CrossPageUpdateDispatchSuffix.Delayed);
    }

    [Fact]
    public void Parse_ShouldFallbackToUnspecified_WhenSourceIsEmpty()
    {
        var result = CrossPageUpdateSourceParser.Parse(string.Empty);

        result.BaseSource.Should().Be(CrossPageUpdateSources.Unspecified);
        result.Suffix.Should().Be(CrossPageUpdateDispatchSuffix.None);
    }

    [Theory]
    [InlineData("-immediate", "Immediate")]
    [InlineData("-delayed", "Delayed")]
    public void Parse_ShouldFallbackToUnspecified_WhenOnlySuffixProvided(
        string source,
        string expectedSuffixName)
    {
        var result = CrossPageUpdateSourceParser.Parse(source);

        result.BaseSource.Should().Be(CrossPageUpdateSources.Unspecified);
        result.Suffix.ToString().Should().Be(expectedSuffixName);
    }

    [Fact]
    public void WithImmediate_ShouldNormalizeExistingDispatchSuffix()
    {
        var source = CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.NeighborRender);
        var normalized = CrossPageUpdateSources.WithImmediate(source);

        normalized.Should().Be(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.NeighborRender));
    }

    [Fact]
    public void WithDelayed_ShouldNormalizeExistingDispatchSuffix()
    {
        var source = CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.NeighborRender);
        var normalized = CrossPageUpdateSources.WithDelayed(source);

        normalized.Should().Be(CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.NeighborRender));
    }

    [Fact]
    public void Normalize_ShouldTrimSource()
    {
        var normalized = CrossPageUpdateSources.Normalize("  ink-state-changed  ");
        normalized.Should().Be(CrossPageUpdateSources.InkStateChanged);
    }

    [Fact]
    public void Normalize_ShouldFallbackToUnspecified_WhenNullOrWhitespace()
    {
        CrossPageUpdateSources.Normalize(null).Should().Be(CrossPageUpdateSources.Unspecified);
        CrossPageUpdateSources.Normalize("   ").Should().Be(CrossPageUpdateSources.Unspecified);
    }
}
