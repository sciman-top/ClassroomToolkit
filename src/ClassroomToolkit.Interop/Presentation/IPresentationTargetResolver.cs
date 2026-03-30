namespace ClassroomToolkit.Interop.Presentation;

public interface IPresentationTargetResolver
{
    PresentationTarget ResolveForeground();

    PresentationTarget ResolvePresentationTarget(
        PresentationClassifier classifier,
        bool allowWps,
        bool allowOffice,
        uint? excludeProcessId = null);
}
