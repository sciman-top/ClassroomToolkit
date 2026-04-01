using System;

namespace ClassroomToolkit.App.Paint;

internal static class InkAutoSaveSnapshotAdmissionPolicy
{
    internal static bool ShouldPersistSnapshot(
        bool runtimeStateKnown,
        string runtimeHash,
        string snapshotHash)
    {
        if (!runtimeStateKnown)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(runtimeHash)
            || string.Equals(runtimeHash, snapshotHash, StringComparison.Ordinal);
    }
}
