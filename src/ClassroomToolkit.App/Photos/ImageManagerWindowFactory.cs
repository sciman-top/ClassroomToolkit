using System.Collections.Generic;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Photos;

public sealed class ImageManagerWindowFactory : IImageManagerWindowFactory
{
    private readonly InkPersistenceService _persistence;

    public ImageManagerWindowFactory(InkPersistenceService persistence)
    {
        _persistence = persistence;
    }

    public ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        var window = new ImageManagerWindow(favorites, recents);
        window.SetInkPersistenceService(_persistence);
        return window;
    }
}
