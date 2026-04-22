using FluentAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayBaselineIntegrityTests
{
    [Fact]
    public void InkReplayScenarioFiles_ShouldHaveMatchingGoldenActionFiles()
    {
        var repoRoot = TestPathHelper.GetRepositoryRootOrThrow();
        var scenarioDir = Path.Combine(repoRoot, "tests", "Baselines", "ink-replay");
        var actionDir = Path.Combine(repoRoot, "tests", "Baselines", "ink-replay-actions");

        Directory.Exists(scenarioDir).Should().BeTrue();
        Directory.Exists(actionDir).Should().BeTrue();

        var scenarioNames = Directory.GetFiles(scenarioDir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        scenarioNames.Should().NotBeEmpty();

        foreach (var scenarioName in scenarioNames)
        {
            var actionFile = Path.Combine(actionDir, $"{scenarioName}.actions.json");
            File.Exists(actionFile).Should().BeTrue(
                $"missing golden action file for scenario '{scenarioName}'");
        }
    }

    [Fact]
    public void InkReplayGoldenActionFiles_ShouldAllBeLoadable()
    {
        var repoRoot = TestPathHelper.GetRepositoryRootOrThrow();
        var actionDir = Path.Combine(repoRoot, "tests", "Baselines", "ink-replay-actions");
        Directory.Exists(actionDir).Should().BeTrue();

        var actionFiles = Directory.GetFiles(actionDir, "*.actions.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        actionFiles.Should().NotBeEmpty();

        foreach (var actionFile in actionFiles)
        {
            var loaded = InkReplayGoldenActionCatalog.Load(actionFile!);
            loaded.Name.Should().NotBeNullOrWhiteSpace($"invalid name in {actionFile}");
            loaded.Actions.Should().NotBeNull($"invalid actions payload in {actionFile}");
            var stem = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(actionFile!));
            loaded.Name.Should().Be(stem, $"golden name should match file stem for {actionFile}");
        }
    }

    [Fact]
    public void InkReplayGoldenActionEntries_ShouldMatchTypeAndSourceConventions()
    {
        var repoRoot = TestPathHelper.GetRepositoryRootOrThrow();
        var actionDir = Path.Combine(repoRoot, "tests", "Baselines", "ink-replay-actions");
        Directory.Exists(actionDir).Should().BeTrue();

        var actionFiles = Directory.GetFiles(actionDir, "*.actions.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        actionFiles.Should().NotBeEmpty();

        foreach (var path in actionFiles)
        {
            var fileName = Path.GetFileName(path);
            var loaded = InkReplayGoldenActionCatalog.Load(fileName);
            foreach (var action in loaded.Actions)
            {
                switch (action.Type)
                {
                    case InkReplayActionType.FastRefreshImmediate:
                        action.Source.Should().NotBeNullOrWhiteSpace($"{fileName}: immediate action source required");
                        action.Source!.Should().EndWith(
                            CrossPageUpdateSources.ImmediateSuffix,
                            $"{fileName}: immediate action must use immediate suffix");
                        break;
                    case InkReplayActionType.DeferredRefreshRequested:
                        action.Source.Should().NotBeNullOrWhiteSpace($"{fileName}: deferred request source required");
                        action.Source!.Should().EndWith(
                            CrossPageUpdateSources.DelayedSuffix,
                            $"{fileName}: deferred request should use delayed suffix");
                        break;
                    case InkReplayActionType.DeferredRefreshScheduled:
                        action.Source.Should().NotBeNullOrWhiteSpace($"{fileName}: deferred schedule source required");
                        action.Source!.Should().NotEndWith(
                            CrossPageUpdateSources.DelayedSuffix,
                            $"{fileName}: deferred scheduled source should be base source");
                        action.Source.Should().NotEndWith(
                            CrossPageUpdateSources.ImmediateSuffix,
                            $"{fileName}: deferred scheduled source should be base source");
                        break;
                    default:
                        action.Source.Should().BeNull($"{fileName}: non-refresh action should not carry source");
                        break;
                }
            }
        }
    }

    [Fact]
    public void InkReplayGoldenActionEntries_ShouldMatchSequenceConventions()
    {
        var repoRoot = TestPathHelper.GetRepositoryRootOrThrow();
        var actionDir = Path.Combine(repoRoot, "tests", "Baselines", "ink-replay-actions");
        Directory.Exists(actionDir).Should().BeTrue();

        var actionFiles = Directory.GetFiles(actionDir, "*.actions.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        actionFiles.Should().NotBeEmpty();

        foreach (var path in actionFiles)
        {
            var fileName = Path.GetFileName(path);
            var loaded = InkReplayGoldenActionCatalog.Load(fileName);
            var actions = loaded.Actions;

            var allowEmpty = loaded.Name.Contains("-skipped", StringComparison.Ordinal)
                || loaded.Name.Contains("-slot-used", StringComparison.Ordinal);
            if (allowEmpty)
            {
                actions.Should().BeEmpty($"{fileName}: skipped/slot-used baseline should stay empty");
                continue;
            }

            actions.Should().NotBeEmpty($"{fileName}: baseline should include expected actions");

            var scheduledCount = actions.Count(a => a.Type == InkReplayActionType.DeferredRefreshScheduled);
            var requestedCount = actions.Count(a => a.Type == InkReplayActionType.DeferredRefreshRequested);
            var immediateCount = actions.Count(a => a.Type == InkReplayActionType.FastRefreshImmediate);

            scheduledCount.Should().BeLessThanOrEqualTo(1, $"{fileName}: deferred scheduled action should be unique");
            requestedCount.Should().BeLessThanOrEqualTo(1, $"{fileName}: deferred request action should be unique");
            immediateCount.Should().BeLessThanOrEqualTo(1, $"{fileName}: immediate action should be unique");

            if (requestedCount > 0)
            {
                scheduledCount.Should().Be(1, $"{fileName}: delayed request should have matching schedule marker");
                var materializedActions = actions.ToList();
                var requestIndex = materializedActions.FindIndex(a => a.Type == InkReplayActionType.DeferredRefreshRequested);
                var scheduledIndex = materializedActions.FindIndex(a => a.Type == InkReplayActionType.DeferredRefreshScheduled);
                requestIndex.Should().BeLessThan(
                    scheduledIndex,
                    $"{fileName}: delayed request should precede schedule marker in current runner contract");

                var requested = materializedActions[requestIndex];
                var scheduled = materializedActions[scheduledIndex];
                var requestedBase = CrossPageUpdateSourceParser.Parse(requested.Source!).BaseSource;
                requestedBase.Should().Be(
                    scheduled.Source,
                    $"{fileName}: delayed request base source should match scheduled source");
            }
        }
    }

    [Fact]
    public void InkReplayScenarioEntries_ShouldMatchStructuralConventions()
    {
        var repoRoot = TestPathHelper.GetRepositoryRootOrThrow();
        var scenarioDir = Path.Combine(repoRoot, "tests", "Baselines", "ink-replay");
        Directory.Exists(scenarioDir).Should().BeTrue();

        var scenarioFiles = Directory.GetFiles(scenarioDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        scenarioFiles.Should().NotBeEmpty();

        foreach (var path in scenarioFiles)
        {
            var fileName = Path.GetFileName(path);
            var scenario = InkReplayFixtureCatalog.Load(fileName);
            var stem = Path.GetFileNameWithoutExtension(fileName);

            scenario.Name.Should().Be(stem, $"{fileName}: scenario name should match file stem");
            scenario.Description.Should().NotBeNullOrWhiteSpace($"{fileName}: scenario description required");
            scenario.Events.Should().NotBeEmpty($"{fileName}: scenario should contain replay events");
            scenario.Events.Should().Contain(
                e => e.Type == InkReplayEventType.PointerUp,
                $"{fileName}: replay scenario should contain pointer-up termination");

            var timestamps = scenario.Events.Select(e => e.TimestampMs).ToArray();
            timestamps.Should().BeInAscendingOrder($"{fileName}: timestamps should be monotonic");
            timestamps.Should().OnlyContain(t => t >= 0, $"{fileName}: timestamps must be non-negative");
            for (var i = 1; i < timestamps.Length; i++)
            {
                timestamps[i].Should().BeGreaterThan(
                    timestamps[i - 1],
                    $"{fileName}: timestamps should be strictly increasing to avoid event-order ambiguity");
            }

            scenario.Events
                .Where(e => e.Pressure.HasValue)
                .Select(e => e.Pressure!.Value)
                .Should()
                .OnlyContain(p => p >= 0.0 && p <= 1.0, $"{fileName}: pressure should be normalized");

            var pointerActive = false;
            var panActive = false;
            foreach (var ev in scenario.Events)
            {
                switch (ev.Type)
                {
                    case InkReplayEventType.PointerDown:
                        pointerActive.Should().BeFalse($"{fileName}: pointer should not down twice without up");
                        pointerActive = true;
                        break;
                    case InkReplayEventType.PointerMove:
                        pointerActive.Should().BeTrue($"{fileName}: pointer move requires active pointer");
                        break;
                    case InkReplayEventType.PointerUp:
                        pointerActive.Should().BeTrue($"{fileName}: pointer up requires active pointer");
                        pointerActive = false;
                        break;
                    case InkReplayEventType.PhotoPanStart:
                        panActive.Should().BeFalse($"{fileName}: photo pan should not start twice");
                        panActive = true;
                        break;
                    case InkReplayEventType.PhotoPanMove:
                        panActive.Should().BeTrue($"{fileName}: photo pan move requires active pan");
                        break;
                    case InkReplayEventType.PhotoPanEnd:
                        panActive.Should().BeTrue($"{fileName}: photo pan end requires active pan");
                        panActive = false;
                        break;
                }
            }

            pointerActive.Should().BeFalse($"{fileName}: pointer sequence should end balanced");
            panActive.Should().BeFalse($"{fileName}: photo pan sequence should end balanced");
        }
    }
}
