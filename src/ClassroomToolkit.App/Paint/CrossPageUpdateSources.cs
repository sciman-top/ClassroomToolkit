namespace ClassroomToolkit.App.Paint;

internal static class CrossPageUpdateSources
{
    internal const string Unspecified = "unspecified";
    internal const string BoardExit = "board-exit";
    internal const string RegionEraseCrossPage = "region-erase-crosspage";
    internal const string InkStateChanged = "ink-state-changed";
    internal const string InkRedrawCompleted = "ink-redraw-completed";
    internal const string InkVisualSyncReplay = "ink-visual-sync-replay";
    internal const string InteractionReplay = "interaction-replay";
    internal const string ManipulationDelta = "manipulation-delta";
    internal const string NavigateInteractiveBrush = "navigate-interactive-brush";
    internal const string NavigateInteractive = "navigate-interactive";
    internal const string NavigateInteractiveFallback = "navigate-interactive-fallback";
    internal const string StepViewport = "step-viewport";
    internal const string ApplyScale = "apply-scale";
    internal const string PhotoPan = "photo-pan";
    internal const string FitWidth = "fit-width";
    internal const string UndoSnapshot = "undo-snapshot";
    internal const string InkShowDisabled = "ink-show-disabled";
    internal const string InkShowEnabled = "ink-show-enabled";
    internal const string InkShowPrefix = "ink-show-";
    internal const string NeighborMissingDelayed = "neighbor-missing-delayed";
    internal const string NeighborSidecar = "neighbor-sidecar";
    internal const string NeighborRender = "neighbor-render";
    internal const string NeighborMissing = "neighbor-missing";
    internal const string PostInput = "post-input";
    internal const string PointerUpFast = "pointer-up-fast";
    internal const string NeighborPrefix = "neighbor-";
    internal const string ImmediateSuffix = "-immediate";
    internal const string DelayedSuffix = "-delayed";

    internal static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Unspecified;
        }

        return source.Trim();
    }

    internal static string WithImmediate(string source)
    {
        var normalized = CrossPageUpdateSourceParser.Parse(source).BaseSource;
        return $"{normalized}{ImmediateSuffix}";
    }

    internal static string WithDelayed(string source)
    {
        var normalized = CrossPageUpdateSourceParser.Parse(source).BaseSource;
        return $"{normalized}{DelayedSuffix}";
    }
}
