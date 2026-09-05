using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Velopack;

namespace ClassroomToolkit.App;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\ClassroomToolkit.SingleInstance";

    // 互斥体必须存活整个进程生命周期；若被 GC 回收，句柄关闭会提前释放单实例锁。
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main()
    {
        // Velopack 更新钩子先于全局异常处理与日志器执行，钩子内异常不允许直接崩溃进程
        //（更新业务可降级，失败原因尽力落盘）。
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TryWriteStartupCrashLog("velopack-hook", ex);
        }

        if (!AcquireSingleInstance())
        {
            return;
        }

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }

    private static bool AcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        ShowTopmostNotice("ClassroomToolkit 已经在运行，请使用已打开的实例。");
        return false;
    }

    private static void ShowTopmostNotice(string text)
    {
        try
        {
            const uint mbIconInformation = 0x40u;
            const uint mbTopmost = 0x40000u;
            _ = MessageBox(IntPtr.Zero, text, "ClassroomToolkit", mbIconInformation | mbTopmost);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // 提示失败也不阻断退出
        }
    }

    private static void TryWriteStartupCrashLog(string stage, Exception ex)
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "ClassroomToolkit");
            Directory.CreateDirectory(directory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{stage}] {ex}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, "startup-crash.log"), line);
        }
        catch (Exception logEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(logEx))
        {
            // 最后退路：连崩溃日志都无法落盘时只能放弃；致命异常直接终止进程。
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
