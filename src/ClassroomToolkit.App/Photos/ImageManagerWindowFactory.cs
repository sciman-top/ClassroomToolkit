using System.Collections.Generic;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Photos;

public sealed class ImageManagerWindowFactory : IImageManagerWindowFactory
{
    private readonly InkPersistenceService _persistence;
    private readonly InkExportService _export;

    public ImageManagerWindowFactory(InkPersistenceService persistence, InkExportService export)
    {
        _persistence = persistence;
        _export = export;
    }

    public ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        var window = new ImageManagerWindow(favorites, recents);
        window.SetInkPersistenceService(_persistence, _export);
        return window;
    }
}
