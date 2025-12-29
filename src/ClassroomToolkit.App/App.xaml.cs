using WpfApplication = System.Windows.Application;
using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 在启动时修复所有 XAML 文件中的 BorderBrush 问题
        try
        {
            XamlFileFixer.FixAllXamlFiles();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"XAML 文件修复失败: {ex.Message}");
        }
        
        // 注册全局 Border 修复
        BorderFixHelper.RegisterGlobalFix();
    }
}
