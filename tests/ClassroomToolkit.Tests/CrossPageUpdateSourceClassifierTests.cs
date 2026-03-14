using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageUpdateSourceClassifierTests
{
    [Theory]
    [InlineData("ink-state-changed", "VisualSync")]
    [InlineData("ink-state-changed-delayed", "VisualSync")]
    [InlineData("ink-redraw-completed", "VisualSync")]
    [InlineData("ink-redraw-completed-immediate", "VisualSync")]
    [InlineData("region-erase-crosspage", "VisualSync")]
    [InlineData("undo-snapshot", "VisualSync")]
    [InlineData("ink-show-enabled", "VisualSync")]
    [InlineData("neighbor-render", "BackgroundRefresh")]
    [InlineData("neighbor-render-delayed", "BackgroundRefresh")]
    [InlineData("neighbor-missing-delayed", "BackgroundRefresh")]
    [InlineData("photo-pan", "Interaction")]
    public void Classify_ShouldMatchExpected(string source, string expectedName)
    {
        CrossPageUpdateSourceClassifier.Classify(source).ToString().Should().Be(expectedName);
    }
}
