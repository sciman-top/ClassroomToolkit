using System.Windows.Media;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageFrameSourceAssignmentPolicy
{
    internal static bool ShouldAssign(
        ImageSource? currentSource,
        ImageSource? nextSource,
        bool forceAssign = false)
    {
        if (forceAssign)
        {
            return true;
        }

        if (nextSource == null)
        {
            return currentSource != null;
        }

        return !ReferenceEquals(currentSource, nextSource);
    }
}
