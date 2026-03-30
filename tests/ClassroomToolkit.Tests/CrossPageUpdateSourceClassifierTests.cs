using ClassroomToolkit.App.Paint;
using FluentAssertions;
using System.Reflection;
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

    [Theory]
    [InlineData(CrossPageUpdateSources.NeighborRender)]
    [InlineData(CrossPageUpdateSources.NeighborMissing)]
    [InlineData(CrossPageUpdateSources.NeighborSidecar)]
    [InlineData(CrossPageUpdateSources.NeighborMissingDelayed)]
    public void Classify_ShouldTreatNeighborSourcesAsBackgroundRefresh_EvenWithDispatchSuffix(string baseSource)
    {
        CrossPageUpdateSourceClassifier.Classify(baseSource)
            .Should().Be(CrossPageUpdateSourceKind.BackgroundRefresh);
        CrossPageUpdateSourceClassifier.Classify(CrossPageUpdateSources.WithImmediate(baseSource))
            .Should().Be(CrossPageUpdateSourceKind.BackgroundRefresh);
        CrossPageUpdateSourceClassifier.Classify(CrossPageUpdateSources.WithDelayed(baseSource))
            .Should().Be(CrossPageUpdateSourceKind.BackgroundRefresh);
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.RegionEraseCrossPage)]
    [InlineData(CrossPageUpdateSources.InkStateChanged)]
    [InlineData(CrossPageUpdateSources.InkRedrawCompleted)]
    [InlineData(CrossPageUpdateSources.UndoSnapshot)]
    [InlineData(CrossPageUpdateSources.InkShowEnabled)]
    [InlineData(CrossPageUpdateSources.InkShowDisabled)]
    public void Classify_ShouldTreatVisualSyncSourcesAsVisualSync_EvenWithDispatchSuffix(string baseSource)
    {
        CrossPageUpdateSourceClassifier.Classify(baseSource)
            .Should().Be(CrossPageUpdateSourceKind.VisualSync);
        CrossPageUpdateSourceClassifier.Classify(CrossPageUpdateSources.WithImmediate(baseSource))
            .Should().Be(CrossPageUpdateSourceKind.VisualSync);
        CrossPageUpdateSourceClassifier.Classify(CrossPageUpdateSources.WithDelayed(baseSource))
            .Should().Be(CrossPageUpdateSourceKind.VisualSync);
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.Unspecified, "Interaction")]
    [InlineData(CrossPageUpdateSources.BoardExit, "Interaction")]
    [InlineData(CrossPageUpdateSources.InteractionReplay, "Interaction")]
    [InlineData(CrossPageUpdateSources.InkVisualSyncReplay, "Interaction")]
    [InlineData(CrossPageUpdateSources.ManipulationDelta, "Interaction")]
    [InlineData(CrossPageUpdateSources.NavigateInteractiveBrush, "Interaction")]
    [InlineData(CrossPageUpdateSources.NavigateInteractive, "Interaction")]
    [InlineData(CrossPageUpdateSources.NavigateInteractiveFallback, "Interaction")]
    [InlineData(CrossPageUpdateSources.StepViewport, "Interaction")]
    [InlineData(CrossPageUpdateSources.ApplyScale, "Interaction")]
    [InlineData(CrossPageUpdateSources.PhotoPan, "Interaction")]
    [InlineData(CrossPageUpdateSources.FitWidth, "Interaction")]
    [InlineData(CrossPageUpdateSources.PostInput, "Interaction")]
    [InlineData(CrossPageUpdateSources.PointerUpFast, "Interaction")]
    public void Classify_ShouldKeepCurrentKindMapping_ForInteractionAndFallbackSources(
        string source,
        string expectedName)
    {
        CrossPageUpdateSourceClassifier.Classify(source).ToString().Should().Be(expectedName);
    }

    [Fact]
    public void Classify_ShouldRequireExplicitCoverage_ForAllBaseSourceConstants()
    {
        var expectedKinds = new Dictionary<string, CrossPageUpdateSourceKind>(StringComparer.Ordinal)
        {
            [CrossPageUpdateSources.Unspecified] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.BoardExit] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.RegionEraseCrossPage] = CrossPageUpdateSourceKind.VisualSync,
            [CrossPageUpdateSources.InkStateChanged] = CrossPageUpdateSourceKind.VisualSync,
            [CrossPageUpdateSources.InkRedrawCompleted] = CrossPageUpdateSourceKind.VisualSync,
            [CrossPageUpdateSources.InkVisualSyncReplay] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.InteractionReplay] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.ManipulationDelta] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.NavigateInteractiveBrush] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.NavigateInteractive] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.NavigateInteractiveFallback] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.StepViewport] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.ApplyScale] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.PhotoPan] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.FitWidth] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.UndoSnapshot] = CrossPageUpdateSourceKind.VisualSync,
            [CrossPageUpdateSources.InkShowDisabled] = CrossPageUpdateSourceKind.VisualSync,
            [CrossPageUpdateSources.InkShowEnabled] = CrossPageUpdateSourceKind.VisualSync,
            [CrossPageUpdateSources.NeighborMissingDelayed] = CrossPageUpdateSourceKind.BackgroundRefresh,
            [CrossPageUpdateSources.NeighborSidecar] = CrossPageUpdateSourceKind.BackgroundRefresh,
            [CrossPageUpdateSources.NeighborRender] = CrossPageUpdateSourceKind.BackgroundRefresh,
            [CrossPageUpdateSources.NeighborMissing] = CrossPageUpdateSourceKind.BackgroundRefresh,
            [CrossPageUpdateSources.PostInput] = CrossPageUpdateSourceKind.Interaction,
            [CrossPageUpdateSources.PointerUpFast] = CrossPageUpdateSourceKind.Interaction
        };

        var discoveredBaseSources = typeof(CrossPageUpdateSources)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Where(f => !f.Name.EndsWith("Prefix", StringComparison.Ordinal))
            .Where(f => !f.Name.EndsWith("Suffix", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        expectedKinds.Keys
            .Should()
            .BeEquivalentTo(discoveredBaseSources,
                "every base source constant should have explicit expected classification");

        foreach (var source in discoveredBaseSources)
        {
            CrossPageUpdateSourceClassifier.Classify(source)
                .Should().Be(expectedKinds[source], $"source '{source}' classification drifted");
        }
    }
}
