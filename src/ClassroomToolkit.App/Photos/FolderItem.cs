namespace ClassroomToolkit.App.Photos;

public sealed record FolderItem(string Path)
{
    public override string ToString()
    {
        return Path;
    }
}
