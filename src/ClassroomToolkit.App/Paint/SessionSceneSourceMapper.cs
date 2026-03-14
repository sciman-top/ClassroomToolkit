using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class SessionSceneSourceMapper
{
    internal static PresentationSourceKind MapPresentationSource(PresentationForegroundSource source)
    {
        return source switch
        {
            PresentationForegroundSource.Wps => PresentationSourceKind.Wps,
            PresentationForegroundSource.Office => PresentationSourceKind.PowerPoint,
            _ => PresentationSourceKind.Unknown
        };
    }

    internal static PhotoSourceKind MapPhotoSource(bool isPdf)
    {
        return isPdf ? PhotoSourceKind.Pdf : PhotoSourceKind.Image;
    }
}
