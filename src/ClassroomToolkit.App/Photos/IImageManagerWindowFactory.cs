using System.Collections.Generic;

namespace ClassroomToolkit.App.Photos;

public interface IImageManagerWindowFactory
{
    ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents);
}
