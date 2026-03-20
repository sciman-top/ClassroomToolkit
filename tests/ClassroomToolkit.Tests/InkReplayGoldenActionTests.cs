using FluentAssertions;
using System.Linq;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayGoldenActionTests
{
    public static IEnumerable<object[]> GoldenCases()
    {
        return InkReplayGoldenRecipeCatalog.GetAll()
            .Select(recipe => new object[] { recipe.FileName });
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task GoldenActions_ShouldMatchBaseline(string actionFileName)
    {
        var recipe = InkReplayGoldenRecipeCatalog.GetAll()
            .Single(x => string.Equals(x.FileName, actionFileName, StringComparison.OrdinalIgnoreCase));
        var actualActions = await recipe.ExecuteAsync();
        var golden = InkReplayGoldenActionCatalog.Load(actionFileName);

        var expectedName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(actionFileName));
        golden.Name.Should().Be(expectedName);
        actualActions.Should().BeEquivalentTo(
            golden.Actions,
            options => options.WithStrictOrdering());
    }
}
