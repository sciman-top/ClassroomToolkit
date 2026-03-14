using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Input;

public static class KeyBindingTokenParser
{
    public static bool TryNormalize(string? text, out string normalized)
    {
        var candidate = (text ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyBindingParser.TryParse(candidate, out var binding) || binding == null)
        {
            normalized = string.Empty;
            return false;
        }

        normalized = binding.ToString();
        return true;
    }
}
