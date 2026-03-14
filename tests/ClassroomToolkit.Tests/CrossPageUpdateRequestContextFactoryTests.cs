using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageUpdateRequestContextFactoryTests
{
    [Fact]
    public void Create_ShouldNormalizeAndParseSource()
    {
        var context = CrossPageUpdateRequestContextFactory.Create("  neighbor-render-delayed  ");

        context.Source.Should().Be("neighbor-render-delayed");
        context.BaseSource.Should().Be(CrossPageUpdateSources.NeighborRender);
        context.Kind.Should().Be(CrossPageUpdateSourceKind.BackgroundRefresh);
    }

    [Fact]
    public void Create_ShouldFallbackToUnspecified_ForEmptyInput()
    {
        var context = CrossPageUpdateRequestContextFactory.Create("   ");

        context.Source.Should().Be(CrossPageUpdateSources.Unspecified);
        context.BaseSource.Should().Be(CrossPageUpdateSources.Unspecified);
        context.Kind.Should().Be(CrossPageUpdateSourceKind.Interaction);
    }
}
