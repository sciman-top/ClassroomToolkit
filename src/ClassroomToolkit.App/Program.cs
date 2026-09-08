using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Velopack;

namespace ClassroomToolkit.App;

internal static class Program
{
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

        // 全局异常处理器要到 OnStartup 才注册：OnStartup 之前的构造/XAML 解析/互斥阶段
        // 失败必须在此兜底，否则静默闪退且无任何日志。
        try
        {
            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TryWriteStartupCrashLog("app-startup", ex);
            ShowTopmostNotice($"ClassroomToolkit 启动失败：{ex.Message}", isError: true);
            Environment.Exit(-1);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "互斥体所有权按设计转移给 _singleInstanceMutex 并持有整个进程生命周期；提前 Dispose 会释放单实例锁。")]
    private static bool AcquireSingleInstance()
    {
        var outcome = Startup.SingleInstanceGate.AcquireWithFallback(
            Startup.SingleInstanceGate.GlobalMutexName,
            Startup.SingleInstanceGate.SessionMutexName,
            out var mutex);
        switch (outcome)
        {
            case Startup.SingleInstanceAcquireOutcome.Acquired:
                _singleInstanceMutex = mutex;
                return true;
            case Startup.SingleInstanceAcquireOutcome.AlreadyRunning:
                NoticeAlreadyRunning();
                return false;
            default:
                // 两级互斥都被 ACL 拒绝（极罕见）：按课堂可用性优先继续启动，落盘留痕。
                TryWriteStartupCrashLog(
                    "single-instance",
                    new InvalidOperationException("Global 与 Local 互斥体均被 ACL 拒绝，已降级为无互斥启动。"));
                return true;
        }
    }

    private static void NoticeAlreadyRunning()
    {
        ShowTopmostNotice("ClassroomToolkit 已经在运行，请使用已打开的实例。", isError: false);
    }

    private static void ShowTopmostNotice(string text, bool isError)
    {
        try
        {
            const uint mbIconInformation = 0x40u;
            const uint mbIconError = 0x10u;
            const uint mbTopmost = 0x40000u;
            var icon = isError ? mbIconError : mbIconInformation;
            _ = MessageBox(IntPtr.Zero, text, "ClassroomToolkit", icon | mbTopmost);
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
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
