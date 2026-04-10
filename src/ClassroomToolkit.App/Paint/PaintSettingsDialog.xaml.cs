using System.Windows;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog : Window
{
    public PaintSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 24);
        MaxWidth = Math.Max(560, SystemParameters.WorkArea.Width - 24);
        
        // 在构造函数中立即修复 BorderBrush 问题
        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                BorderFixHelper.FixAllBorders(this);
                System.Diagnostics.Debug.WriteLine("PaintSettingsDialog: 构造函数中修复完成");
            },
            ex => System.Diagnostics.Debug.WriteLine($"PaintSettingsDialog 构造函数修复失败: {ex.Message}"));

        _presetRecommendation = PresetSchemePolicy.ResolveRecommendation(settings);
        InitializeFromSettings(settings);
    }

}
