namespace ClassroomToolkit.Interop.Presentation;

public sealed record KeyBinding(VirtualKey Key, KeyModifiers Modifiers)
{
    public override string ToString()
    {
        if (Modifiers == KeyModifiers.None)
        {
            return Key.ToString().ToLowerInvariant();
        }
        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("ctrl");
        }
        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("shift");
        }
        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("alt");
        }
        parts.Add(Key.ToString().ToLowerInvariant());
        return string.Join('+', parts);
    }
}
