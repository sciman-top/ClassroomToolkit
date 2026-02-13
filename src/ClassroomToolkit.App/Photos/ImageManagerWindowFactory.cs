using System.Collections.Generic;

namespace ClassroomToolkit.App.Photos;

public sealed class ImageManagerWindowFactory : IImageManagerWindowFactory
{
    public ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        return new ImageManagerWindow(favorites, recents);
    }
}
