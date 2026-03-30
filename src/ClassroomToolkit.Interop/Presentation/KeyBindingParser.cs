namespace ClassroomToolkit.Interop.Presentation;

public static class KeyBindingParser
{
    private static readonly Dictionary<string, VirtualKey> TokenMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tab"] = VirtualKey.Tab,
        ["enter"] = VirtualKey.Enter,
        ["space"] = VirtualKey.Space,
        ["esc"] = VirtualKey.Escape,
        ["escape"] = VirtualKey.Escape,
        ["pageup"] = VirtualKey.PageUp,
        ["pagedown"] = VirtualKey.PageDown,
        ["pgup"] = VirtualKey.PageUp,
        ["pgdn"] = VirtualKey.PageDown,
        ["left"] = VirtualKey.Left,
        ["right"] = VirtualKey.Right,
        ["up"] = VirtualKey.Up,
        ["down"] = VirtualKey.Down,
        ["b"] = VirtualKey.B,
        ["w"] = VirtualKey.W
    };

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
        bool TryAssignKey(VirtualKey candidate)
        {
            if (key.HasValue && key.Value != candidate)
            {
                return false;
            }
            key = candidate;
            return true;
        }
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
                default:
                    if (TokenMap.TryGetValue(token, out var mapped))
                    {
                        if (!TryAssignKey(mapped))
                        {
                            return false;
                        }
                        break;
                    }
                    if (TryParseAlphaNumeric(token, out var alphaNumeric))
                    {
                        if (!TryAssignKey(alphaNumeric))
                        {
                            return false;
                        }
                        break;
                    }
                    if (TryParseFunctionKey(token, out var functionKey))
                    {
                        if (!TryAssignKey(functionKey))
                        {
                            return false;
                        }
                    }
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

    private static bool TryParseAlphaNumeric(string token, out VirtualKey key)
    {
        key = default;
        if (token.Length != 1)
        {
            return false;
        }
        var ch = token[0];
        if (ch >= 'a' && ch <= 'z')
        {
            key = (VirtualKey)((int)VirtualKey.A + (ch - 'a'));
            return true;
        }
        if (ch >= '0' && ch <= '9')
        {
            key = (VirtualKey)((int)VirtualKey.D0 + (ch - '0'));
            return true;
        }
        return false;
    }

    private static bool TryParseFunctionKey(string token, out VirtualKey key)
    {
        key = default;
        if (!token.StartsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!int.TryParse(token[1..], out var number))
        {
            return false;
        }
        if (number is < 1 or > 12)
        {
            return false;
        }
        key = (VirtualKey)((int)VirtualKey.F1 + (number - 1));
        return true;
    }
}
