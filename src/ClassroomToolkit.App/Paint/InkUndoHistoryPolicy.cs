namespace ClassroomToolkit.App.Paint;

internal static class InkUndoHistoryPolicy
{
    internal static bool ShouldTrackVectorSnapshot(bool inkRecordEnabled, bool photoInkModeActive)
    {
        return inkRecordEnabled || photoInkModeActive;
    }

    internal static bool ShouldPreferGlobalPhotoUndo(bool photoModeActive, int globalHistoryCount)
    {
        return photoModeActive && globalHistoryCount > 0;
    }

    internal static bool ShouldPreferLocalVectorUndo(
        bool inkRecordEnabled,
        bool photoInkModeActive,
        int localHistoryCount)
    {
        return localHistoryCount > 0
            && ShouldTrackVectorSnapshot(inkRecordEnabled, photoInkModeActive);
    }
}
