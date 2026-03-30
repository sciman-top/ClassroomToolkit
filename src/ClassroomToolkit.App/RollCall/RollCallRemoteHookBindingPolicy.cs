namespace ClassroomToolkit.App.RollCall;

internal static class RollCallRemoteHookBindingPolicy
{
    internal static IReadOnlyList<string> ResolveTokens(string configuredKey, string fallbackToken)
    {
        if (string.Equals(configuredKey?.Trim(), "f5", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "f5",
                "shift+f5",
                "escape"
            ];
        }

        var token = string.IsNullOrWhiteSpace(configuredKey)
            ? fallbackToken
            : configuredKey.Trim();
        if (IsUnsupportedToken(token))
        {
            token = fallbackToken;
        }

        return [token];
    }

    private static bool IsUnsupportedToken(string token)
    {
        return string.Equals(token, "w", StringComparison.OrdinalIgnoreCase); // W (removed)
    }
}
