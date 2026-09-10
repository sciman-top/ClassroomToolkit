using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class WpsPresentationRuntimePolicy
{
    internal static bool IsDedicatedSlideshowRuntime(string? processName)
    {
        return PresentationClassifier.IsDedicatedWpsPresentationRuntime(processName);
    }
}
