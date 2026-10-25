using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private static readonly (string Label, string Value)[] WpsModeChoices =
    {
        ("自动判断（推荐）", WpsInputModeDefaults.Auto),
        ("兼容优先（PostMessage）", WpsInputModeDefaults.Message),
        ("性能优先（SendInput）", WpsInputModeDefaults.Raw)
    };

    private static readonly (string Label, string Value)[] OfficeModeChoices =
    {
        ("自动判断（推荐）", WpsInputModeDefaults.Auto),
        ("强制原始输入（SendInput）", WpsInputModeDefaults.Raw),
        ("兼容排障（PostMessage，画笔态可能无效）", WpsInputModeDefaults.Message)
    };

    private static readonly (string Label, int Value)[] WpsDebounceChoices =
    {
        ("关闭（0ms）", 0),
        ("80 ms（更灵敏）", 80),
        ("120 ms（推荐）", 120),
        ("160 ms（更稳）", 160),
        ("200 ms（更稳）", 200),
        ("300 ms（更稳）", 300)
    };

    private static readonly (string Label, int Value)[] FallbackFailureThresholdChoices =
    {
        ("1 次（激进）", 1),
        ("2 次（推荐）", 2),
        ("3 次（保守）", 3),
        ("4 次（更保守）", 4)
    };

    private static readonly (string Label, int Value)[] FallbackProbeIntervalChoices =
    {
        ("4 次（更快恢复）", 4),
        ("8 次（推荐）", 8),
        ("12 次（更稳）", 12),
        ("16 次（更稳）", 16)
    };
}
