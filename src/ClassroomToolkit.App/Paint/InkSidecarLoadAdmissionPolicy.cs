using System;

namespace ClassroomToolkit.App.Paint;

internal static class InkSidecarLoadAdmissionPolicy
{
    internal static bool ShouldApplyLoadedSnapshot(
        bool runtimeStateKnown,
        string runtimeHash,
        bool runtimeDirty,
        string loadedHash)
    {
        if (!runtimeStateKnown || string.IsNullOrWhiteSpace(runtimeHash))
        {
            return true;
        }

        var hashMatched = string.Equals(runtimeHash, loadedHash, StringComparison.Ordinal);
        if (hashMatched)
        {
            return true;
        }

        if (runtimeDirty)
        {
            return false;
        }

        return !string.Equals(runtimeHash, "empty", StringComparison.Ordinal);
    }
}
