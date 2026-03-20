using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayGoldenSnapshotDiffTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static InkReplayGoldenSnapshotDiffTests()
    {
        SnapshotJsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public async Task GeneratedSnapshots_ShouldMatchCurrentGoldenBaselines()
    {
        var recipes = BuildRecipes();
        var actionDir = TestPathHelper.ResolveRepoPath("tests", "Baselines", "ink-replay-actions");
        var baselineFiles = Directory.GetFiles(actionDir, "*.actions.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        recipes.Keys.Should().BeEquivalentTo(
            baselineFiles,
            "every golden baseline should have a snapshot generation recipe");

        foreach (var fileName in baselineFiles)
        {
            var expected = NormalizeBaselineJson(File.ReadAllText(Path.Combine(actionDir, fileName!)));
            var actualActions = await recipes[fileName!]();
            var generated = NormalizeBaselineJson(
                JsonSerializer.Serialize(
                    new InkReplayGoldenActions(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName!)), actualActions),
                    SnapshotJsonOptions));

            generated.Should().Be(
                expected,
                $"{fileName}: generated snapshot drifted from current baseline");
        }
    }

    private static Dictionary<string, Func<Task<IReadOnlyList<InkReplayAction>>>> BuildRecipes()
    {
        return new Dictionary<string, Func<Task<IReadOnlyList<InkReplayAction>>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["crosspage-seam-basic.actions.json"] = () => Task.FromResult(
                InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
                    InkReplayFixtureCatalog.Load("crosspage-seam-basic.json"),
                    crossPageDisplayActive: true,
                    pendingInkContextCheck: true,
                    updatePending: false,
                    crossPageFirstInputTraceActive: true).Actions),
            ["crosspage-dpi-switch-pointerup.actions.json"] = () => Task.FromResult(
                InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
                    InkReplayFixtureCatalog.Load("crosspage-dpi-switch-pointerup.json"),
                    crossPageDisplayActive: true,
                    pendingInkContextCheck: true,
                    updatePending: false,
                    crossPageFirstInputTraceActive: true).Actions),
            ["crosspage-rapid-flip-pointerup.actions.json"] = () => Task.FromResult(
                InkReplayScenarioRunner.RunCrossPagePointerUpPipeline(
                    InkReplayFixtureCatalog.Load("crosspage-rapid-flip-pointerup.json"),
                    crossPageDisplayActive: true,
                    pendingInkContextCheck: true,
                    updatePending: false,
                    crossPageFirstInputTraceActive: true).Actions),
            ["crosspage-neighbor-render-postinput.actions.json"] = () => RunDeferredAsync(CrossPageUpdateSources.NeighborRender, 60, 10),
            ["crosspage-postinput-immediate.actions.json"] = () => RunDeferredAsync(CrossPageUpdateSources.PostInput, 120, 500),
            ["crosspage-neighbor-render-recover.actions.json"] = () => RunDeferredAsync(
                CrossPageUpdateSources.NeighborRender,
                60,
                10,
                forceDispatchFailure: true,
                dispatcherCheckAccess: true),
            ["crosspage-neighbor-render-skipped.actions.json"] = () => RunDeferredAsync(
                CrossPageUpdateSources.NeighborRender,
                60,
                10,
                crossPageInteractionActive: true),
            ["crosspage-interaction-replay-delayed.actions.json"] = () => RunDeferredAsync(CrossPageUpdateSources.InteractionReplay, 80, 10),
            ["crosspage-interaction-replay-delay-failure-recover.actions.json"] = () => RunDeferredAsync(
                CrossPageUpdateSources.InteractionReplay,
                80,
                10,
                forceDelayFailure: true,
                dispatcherCheckAccess: true),
            ["crosspage-postinput-slot-used.actions.json"] = () => RunDeferredAsync(
                CrossPageUpdateSources.PostInput,
                120,
                500,
                initialAppliedSequence: 1),
            ["crosspage-interaction-replay-delay-failure-abort.actions.json"] = () => RunDeferredAsync(
                CrossPageUpdateSources.InteractionReplay,
                80,
                10,
                forceDelayFailure: true,
                forceDispatchFailure: true,
                dispatcherCheckAccess: false),
            ["crosspage-neighbor-missing-delayed.actions.json"] = () => RunDeferredAsync(CrossPageUpdateSources.NeighborMissing, 80, 10),
            ["crosspage-ink-visual-sync-replay-delayed.actions.json"] = () => RunDeferredAsync(CrossPageUpdateSources.InkVisualSyncReplay, 80, 10),
            ["crosspage-neighbor-sidecar-delayed.actions.json"] = () => RunDeferredAsync(CrossPageUpdateSources.NeighborSidecar, 80, 10)
        };
    }

    private static async Task<IReadOnlyList<InkReplayAction>> RunDeferredAsync(
        string source,
        int configuredDelayMs,
        int elapsedSincePointerUpMs,
        bool crossPageDisplayActive = true,
        bool crossPageInteractionActive = false,
        bool forceDelayFailure = false,
        bool forceDispatchFailure = false,
        bool dispatcherCheckAccess = true,
        long initialAppliedSequence = 0)
    {
        var scenario = InkReplayFixtureCatalog.Load("crosspage-neighbor-render-postinput.json");
        var result = await InkReplayScenarioRunner.RunDeferredRefreshCoordinatorPipelineAsync(
            scenario,
            source: source,
            singlePerPointerUp: true,
            configuredDelayMs: configuredDelayMs,
            delayOverrideMs: null,
            elapsedSincePointerUpMs: elapsedSincePointerUpMs,
            crossPageDisplayActive: crossPageDisplayActive,
            crossPageInteractionActive: crossPageInteractionActive,
            dispatchSynchronously: true,
            forceDelayFailure: forceDelayFailure,
            forceDispatchFailure: forceDispatchFailure,
            dispatcherCheckAccess: dispatcherCheckAccess,
            initialAppliedSequence: initialAppliedSequence);
        return result.Actions;
    }

    private static string NormalizeBaselineJson(string json)
    {
        var model = JsonSerializer.Deserialize<InkReplayGoldenActions>(json, SnapshotJsonOptions);
        model.Should().NotBeNull();
        return JsonSerializer.Serialize(model, SnapshotJsonOptions);
    }
}
