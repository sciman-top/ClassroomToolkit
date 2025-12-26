namespace ClassroomToolkit.Interop.Presentation;

public static class KeyBindingParser
{
    public static bool TryParse(string? value, out KeyBinding? binding)
    {
        binding = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        var tokens = value
            .Trim()
            .ToLowerInvariant()
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var modifiers = KeyModifiers.None;
        VirtualKey? key = null;
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "shift":
                    modifiers |= KeyModifiers.Shift;
                    break;
                case "ctrl":
                case "control":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "alt":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "tab":
                    key = VirtualKey.Tab;
                    break;
                case "b":
                    key = VirtualKey.B;
                    break;
                case "w":
                    key = VirtualKey.W;
                    break;
            }
        }
        if (key == null)
        {
            return false;
        }
        binding = new KeyBinding(key.Value, modifiers);
        return true;
    }

    public static KeyBinding ParseOrDefault(string? value, KeyBinding fallback)
    {
        return TryParse(value, out var binding) ? binding! : fallback;
    }
}
