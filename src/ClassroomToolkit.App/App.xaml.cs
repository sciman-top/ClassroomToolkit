using WpfApplication = System.Windows.Application;
using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 在启动时立即修复所有 BorderBrush 问题
        try
        {
            GlobalBorderFixer.FixAllBordersImmediately();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"全局 Border 修复失败: {ex.Message}");
        }
        
        // 注册全局 Border 修复
        BorderFixHelper.RegisterGlobalFix();
    }
}
