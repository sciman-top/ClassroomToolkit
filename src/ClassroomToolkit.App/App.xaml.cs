using WpfApplication = System.Windows.Application;
using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 注册全局 Border 修复
        BorderFixHelper.RegisterGlobalFix();
    }
}
