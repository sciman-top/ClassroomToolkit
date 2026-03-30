namespace ClassroomToolkit.App.Diagnostics;

public sealed record DiagnosticsResult(bool HasIssues, string Title, string Detail, string Suggestion)
{
    public string Summary => HasIssues
        ? "检测到系统环境存在潜在兼容性问题。"
        : "系统环境检测正常。";
}
