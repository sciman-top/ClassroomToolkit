namespace ClassroomToolkit.App.Models;

public sealed class GroupButtonItem
{
    public GroupButtonItem(string label, bool isReset)
    {
        Label = label ?? string.Empty;
        IsReset = isReset;
    }

    public string Label { get; }

    public bool IsReset { get; }
}
