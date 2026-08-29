namespace ClassroomToolkit.App.UI.Themes;

public static class ThemePreferenceService
{
    public const AppTheme DefaultTheme = AppTheme.MidnightTeal;

    public static AppTheme Parse(string? value)
    {
        return Enum.TryParse<AppTheme>(value?.Trim(), ignoreCase: true, out var theme)
            && Enum.IsDefined(theme)
            ? theme
            : DefaultTheme;
    }

    public static string Normalize(string? value)
    {
        return Parse(value).ToString();
    }

    public static string GetDisplayName(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Blackboard => "黑板护眼",
            AppTheme.Light => "明亮",
            _ => "课堂深色（推荐）"
        };
    }
}
