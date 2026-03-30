namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageNeighborInkFrameDecision(
    bool ClearCurrentFrame,
    bool AllowResolvedInkReplacement,
    bool KeepVisible);
