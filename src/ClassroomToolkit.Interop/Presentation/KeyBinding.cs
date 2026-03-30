namespace ClassroomToolkit.Interop.Presentation;

public sealed record KeyBinding(VirtualKey Key, KeyModifiers Modifiers)
{
    public override string ToString()
    {
        if (Modifiers == KeyModifiers.None)
        {
            return KeyToToken(Key);
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
        parts.Add(KeyToToken(Key));
        return string.Join('+', parts);
    }

    private static string KeyToToken(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.PageDown => "pagedown",
            VirtualKey.PageUp => "pageup",
            VirtualKey.Space => "space",
            VirtualKey.Enter => "enter",
            VirtualKey.Escape => "esc",
            VirtualKey.Left => "left",
            VirtualKey.Right => "right",
            VirtualKey.Up => "up",
            VirtualKey.Down => "down",
            VirtualKey.D0 => "0",
            VirtualKey.D1 => "1",
            VirtualKey.D2 => "2",
            VirtualKey.D3 => "3",
            VirtualKey.D4 => "4",
            VirtualKey.D5 => "5",
            VirtualKey.D6 => "6",
            VirtualKey.D7 => "7",
            VirtualKey.D8 => "8",
            VirtualKey.D9 => "9",
            _ => key.ToString().ToLowerInvariant()
        };
    }
}
