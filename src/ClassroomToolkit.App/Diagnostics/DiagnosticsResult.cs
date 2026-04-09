namespace ClassroomToolkit.App.Diagnostics;

public sealed record DiagnosticsResult(
    bool HasIssues,
    string Title,
    string Detail,
    string Suggestion,
    string HealthBadge)
{
    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(HealthBadge))
            {
                return HasIssues
                    ? "检测到潜在兼容问题。"
                    : "系统环境检测正常。";
            }

            return HasIssues
                ? $"{HealthBadge}（检测到潜在兼容问题）"
                : $"{HealthBadge}（系统环境检测正常）";
        }
    }
}
