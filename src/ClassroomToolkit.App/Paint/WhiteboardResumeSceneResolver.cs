using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Presentation;

namespace ClassroomToolkit.App.Paint;

internal sealed record WhiteboardResumeScene(
    UiSceneKind Scene,
    PhotoSourceKind PhotoSource,
    PresentationSourceKind PresentationSource);

internal static class WhiteboardResumeSceneResolver
{
    internal static WhiteboardResumeScene Resolve(
        bool photoModeActive,
        bool photoIsPdf,
        PresentationForegroundSource fullscreenPresentationSource)
    {
        if (photoModeActive)
        {
            return new WhiteboardResumeScene(
                UiSceneKind.PhotoFullscreen,
                SessionSceneSourceMapper.MapPhotoSource(photoIsPdf),
                PresentationSourceKind.Unknown);
        }

        if (fullscreenPresentationSource != PresentationForegroundSource.Unknown)
        {
            return new WhiteboardResumeScene(
                UiSceneKind.PresentationFullscreen,
                PhotoSourceKind.Unknown,
                SessionSceneSourceMapper.MapPresentationSource(fullscreenPresentationSource));
        }

        return new WhiteboardResumeScene(
            UiSceneKind.Idle,
            PhotoSourceKind.Unknown,
            PresentationSourceKind.Unknown);
    }
}
